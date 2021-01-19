using EnterpriseDT.Net.Ftp;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.Odbc;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;

namespace FTP_Attestation_CopyFiles
{
    class Program
    {
        public static List<string> Local_long_listFiles;
        public static List<string> Local_Short_listFiles;
        public static string dir_Name;
        public static string serverAddress;
        public static string userName;
        public static string password;
        public static FTPConnection ftp;

        public static string connectionString;
        public static DateTime firstDate;                 // Дата создания директории
        public static DateTime lastDate;                  // Дата для базы данных
        public static List<string> part;

        public static void log(string message) // запись логов в текстовый файл
        {
            using (StreamWriter sw = File.AppendText("FTP_Attestation_CopyFiles.log.txt"))
            {
                sw.WriteLine(message);
            }
        }
        public static void InsertRow(string connectionString, DateTime dateTime) // соединение с базой данных по ODBC
        {
            string dat = (dateTime.ToUniversalTime().ToString("u")).Replace("Z", "");
            //string query = $"select part_id from incube.dbo.tb_part where end_time>'{dat}'";                   // реальный код 
            string query = $"select part_id from incube.dbo.tb_part where end_time>'2020-10-15 12:00:00.000'";   // тестовый код
            using (OdbcConnection connection = new OdbcConnection(connectionString))
            {
                OdbcCommand command = new OdbcCommand(query, connection);
                try
                {
                    connection.Open();
                    Console.WriteLine("Соединение с базой данных установлено");
                    part = new List<string>();
                    // Запустите DataReader и получите доступ к данным.
                    OdbcDataReader reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        part.Add(reader[0].ToString());
                    }
                    if(part.Count > 0)
                    {
                        Console.WriteLine("Получили список из базы данных");
                    }
                    else
                    {
                        Console.WriteLine("Список партий из базы данных пустой");
                    }
                    //DateTime dateTime1 = DateTime.Now;
                    // убираем букву Z из конца строки даты
                    //string dat = (dateTime1.ToUniversalTime().ToString("u")).Replace("Z", ""); 
                    //Console.WriteLine(dat);
                    //Console.Read();
                    // Call Close when done reading.
                    reader.Close();
                }
                catch (Exception ss)
                {
                    log(ss.ToString()+" Чтение из базы" + DateTime.Now.ToString());
                }
            }
        }
       
        static void Main(string[] args)
        {
            Local_long_listFiles = new List<string>();
            Local_Short_listFiles = new List<string>();

            // Чтение конфигурации из файла
            try
            {
                XmlDocument xDoc = new XmlDocument();
                xDoc.Load("job.conf");
                // получим корневой элемент
                XmlElement xRoot = xDoc.DocumentElement;
                // обход всех узлов в корневом элементе
                foreach (XmlNode xnode in xRoot)
                {
                    var v = xnode.Name;
                    if (xnode.Name == "ftp_att_copy")
                    {
                        foreach (XmlNode childnode in xnode.ChildNodes)
                        {
                            foreach (XmlNode atreb in childnode.Attributes)
                            {
                                switch (atreb.Name)
                                {
                                    case "dirName":
                                        dir_Name = atreb.Value;
                                        break;
                                    case "ServerAddress":
                                        serverAddress = atreb.Value;
                                        break;
                                    case "UserName":
                                        userName = atreb.Value;
                                        break;
                                    case "Password":
                                        password = atreb.Value;
                                        break;
                                }
                            }
                        }
                    }
                    else if (xnode.Name == "connection_DB")
                    {
                        foreach (XmlNode childnode in xnode.ChildNodes)
                        {
                            foreach (XmlNode atreb in childnode.Attributes)
                            {
                                if (atreb.Name == "connectionString")
                                {
                                    connectionString = atreb.Value;
                                }
                            }
                        }
                    }
                }
                // соединение с сервером по FTP 
                ftp = new FTPConnection();
                ftp.ConnectMode = FTPConnectMode.ACTIVE;
                ftp.ServerAddress = serverAddress;
                ftp.UserName = userName;
                ftp.Password = password;
                ftp.Connect();
                Console.WriteLine("Соединение с FTP установлено");
            }
            catch (Exception a)
            {
                log(DateTime.Now.ToString() +" Чтение конфига" + a.ToString() );
            }

            // Получение списка директорий на сервере ////////////////////////////////////////////////////////////////////////////////////////////////
            FTPFile[] GUID_directory = ftp.GetFileInfos();

            // сортировка директории по дате создания (первая самая новая)
            FTPFile temp;
            for (int i = 0; i < GUID_directory.Length - 1; i++)
            {
                for (int j = i + 1; j < GUID_directory.Length; j++)
                {
                    //Console.WriteLine(GUID_directory[j].LastModified);
                    if (GUID_directory[i].LastModified < GUID_directory[j].LastModified)
                    {
                        temp = GUID_directory[i];
                        GUID_directory[i] = GUID_directory[j];
                        GUID_directory[j] = temp;
                    }
                }
            }

            firstDate = GUID_directory[0].LastModified;
            lastDate = firstDate.AddHours(5);
            Console.WriteLine("Получили дату для выборки из базы данных");

            // Копирование и удаление файлов и директорий ////////////////////////////////////////////////////////////////////////////////////////////
            if (GUID_directory.Length > 0)
            {
                try
                {
                    InsertRow(connectionString, lastDate); // получаем список партий из базы
                }
                catch (Exception aa)
                {
                    log(DateTime.Now.ToString() + " Получение списка партий из базы " + aa.ToString());
                }
                for (int i = 0; i < GUID_directory.Length; i++)
                {
                    foreach (string pr in part)
                    {
                        try
                        {
                            if (pr.Equals(GUID_directory[i].Name, StringComparison.OrdinalIgnoreCase) )  // если совпадает номер на сервере и в базе (значить партия закрыта)
                            {
                                // создаем папку на вычислителе с именем партии
                                DirectoryInfo dirInfo = new DirectoryInfo(dir_Name + "/" + GUID_directory[i].Name);
                                if (!dirInfo.Exists)
                                {
                                    dirInfo.Create();
                                    Console.WriteLine($"Создали папки партии {GUID_directory[i].Name} на жестком диске");
                                }
                                try
                                {
                                    ftp.ChangeWorkingDirectory("/"+GUID_directory[i].Name);     // сменить рабочую директорию, войти в папку партии
                                }
                                catch (Exception aa)
                                {
                                    Console.WriteLine("ftp_set_work_dir="+GUID_directory[i].Name +" : "+aa.ToString());
                                    log(DateTime.Now.ToString() + "  " + aa.ToString());
                                }
                                FTPFile[] vag_Number = ftp.GetFileInfos();               // файлы в папке партии
                                if (vag_Number.Length > 0)
                                {
                                    for (int k = 0; k < vag_Number.Length; k++)
                                    {
                                        if (vag_Number[k].Dir)  // true если это директория
                                        {
                                            dirInfo.CreateSubdirectory(vag_Number[k].Name);             // создаем папку на вычислителе с номером вагона
                                            try
                                            {
                                                ftp.ChangeWorkingDirectory("/" + GUID_directory[i].Name+"/"+vag_Number[k].Name);             // сменить рабочую директорию, войти в папку вагона
                                            }
                                            catch (Exception aa)
                                            {
                                                Console.WriteLine(aa.ToString());
                                                log(DateTime.Now.ToString() + "  " + aa.ToString());
                                            }
                                            FTPFile[] files = ftp.GetFileInfos();                      // файлы в папке вагона
                                            if (files.Length > 0)
                                            {
                                                int lngth = files.Length;
                                                foreach (FTPFile a in files)
                                                {
                                                    try
                                                    {
                                                        // копирование файла с сервера на компьютер
                                                        ftp.DownloadFile(dir_Name + "/" + GUID_directory[i].Name + "/" + vag_Number[k].Name + "/" + a.Name, a.Name);
                                                        //Console.WriteLine($"Файл с фото {a.Name} скопирован");
                                                        // сравниваем размер скаченного файла на диске и сервере
                                                        FileInfo info = new FileInfo(dir_Name + "/" + GUID_directory[i].Name + "/" + vag_Number[k].Name + "/" + a.Name);
                                                        if (info.Length == a.Size)
                                                        {
                                                            ftp.DeleteFile(a.Name);                // удаляем файл
                                                            //Console.WriteLine("Фото удалено с сервера");
                                                        }
                                                        else
                                                        {
                                                            Console.WriteLine($"Не удалось удалить фото {a.Name} после копирования");
                                                        }
                                                    }
                                                    catch (Exception kk)
                                                    {
                                                        log(DateTime.Now.ToString()+"  Копирование фотографий" + kk.ToString() );
                                                    }
                                                }
                                            }
                                            ftp.ChangeWorkingDirectory("/" + GUID_directory[i].Name);     // сменить рабочую директорию, войти в папку партии
                                            //ftp.ChangeWorkingDirectoryUp();                // вернуться из папки
                                            try
                                            {
                                                ftp.DeleteDirectory(vag_Number[k].Name);        // удалить папку вагона
                                                Console.WriteLine("Удалена папка с номером вагона");
                                            }
                                            catch (Exception jj)
                                            {
                                                Console.WriteLine("Не удалось удалить папку с номером вагона");
                                                log(DateTime.Now.ToString() +"  Удаление папки с номером вагона" + jj.ToString() );
                                            }
                                            //ftp.ChangeWorkingDirectory("/");     // сменить рабочую директорию, войти в папку партии
                                        }
                                        else
                                        {
                                            try
                                            {
                                                // копируем файл с видео
                                                ftp.DownloadFile(dir_Name + "/" + GUID_directory[i].Name + "/" + vag_Number[k].Name, vag_Number[k].Name);
                                                Console.WriteLine($"Файл с видео {vag_Number[k].Name} скопирован");
                                                FileInfo info = new FileInfo(dir_Name + "/" + GUID_directory[i].Name + "/" + vag_Number[k].Name);
                                                // сравниваем размер
                                                if (info.Length == vag_Number[k].Size)
                                                {
                                                    ftp.DeleteFile(vag_Number[k].Name);        // удаляем файл
                                                    Console.WriteLine($"Удалили файл видео {vag_Number[k].Name} с сервера");
                                                }
                                                else
                                                {
                                                    Console.WriteLine($"Не удалось удалить файл видео {vag_Number[k].Name} с сервера");
                                                }
                                            }
                                            catch (Exception jj)
                                            {
                                                Console.WriteLine("Ошибка копирования видео");
                                                log(DateTime.Now.ToString() +" Копирование видео" + jj.ToString() );
                                            }
                                        }
                                    }
                                    ftp.ChangeWorkingDirectory("/");     // сменить рабочую директорию, войти в папку партии
                                    //ftp.ChangeWorkingDirectoryUp();                       // вернуться из папки партии
                                    try
                                    {
                                        ftp.DeleteDirectory(GUID_directory[i].Name);      // удалить папку партии
                                        Console.WriteLine("Папка партии удалена с сервера");
                                    }
                                    catch (Exception jj)
                                    {
                                        Console.WriteLine("Ошибка удаления папки партии с сервера");
                                        log(DateTime.Now.ToString() + "  " +jj.ToString() );
                                    }
                                }
                                else
                                {
                                    ftp.ChangeWorkingDirectory("/");     // сменить рабочую директорию, войти в папку партии
                                    try
                                    {
                                        ftp.DeleteDirectory(GUID_directory[i].Name);      // удалить папку партии
                                        Console.WriteLine("Папка партии удалена с сервера");
                                    }
                                    catch (Exception jj)
                                    {
                                        Console.WriteLine("Ошибка удаления папки партии с сервера");
                                        log(DateTime.Now.ToString() + "  " + jj.ToString());
                                    }
                                }
                            }
                        }
                        catch (Exception dd)
                        {
                            Console.WriteLine("Ошибка в процессе копирования и удаления");
                            log(DateTime.Now.ToString() + "  Ошибка в процессе копирования и удаления" + dd.ToString());
                        }
                    }
                }
                Console.WriteLine("Все доступные партии проработаны");
                //Console.WriteLine("Старые директории удалены");
            }
            try
            {
                ftp.Close();
                Console.WriteLine("Соединение с FTP успешно закрыто");
            }
            catch (Exception ff)
            {
                Console.WriteLine("Ошибка закрытия соединения FTP");
                log(DateTime.Now.ToString() + " Ошибка закрытия соединения FTP" + ff.ToString() );
            }
            //Console.Read();
            Thread.Sleep(200000);
        }
    }
}
