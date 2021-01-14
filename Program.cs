using EnterpriseDT.Net.Ftp;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace FTP_Attestation_CopyFiles
{
    class Program
    {
        public static List<string> Local_long_listFiles;
        public static List<string> Local_Short_listFiles;
        public static string[] main_Long_files;
        public static string[] main_Short_files;
        public static string dir_Name;
        public static string serverAddress;
        public static string userName;
        public static string password;

        public static void log(string message) // запись логов в текстовый файл
        {
            using (StreamWriter sw = File.AppendText("FTP_Attestation_CopyFiles.log.txt"))
            {
                sw.WriteLine(message);
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
                xDoc.Load("Jobs_config.xml");
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
                }
            }
            catch (Exception a)
            {
                log(a.ToString());
            }
            // ///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

            string dirName = ConfigurationManager.AppSettings["dirName"];


            if (Directory.Exists(dirName))
            {
                Console.WriteLine("Подкаталоги:");
                string[] dirs_ = Directory.GetDirectories(dirName);
                for (int i = 0; i < dirs_.Length; i++)
                {
                    Local_long_listFiles.Add(dirs_[i]);
                    dirs_[i] = new FileInfo(dirs_[i]).Name; // Выделяем короткое название из пути
                    Local_Short_listFiles.Add(dirs_[i]);

                    Console.WriteLine(Local_long_listFiles[i]);
                    Console.WriteLine(Local_Short_listFiles[i]);

                    /* string[] files = Directory.GetFiles(s);
                     foreach (string p in files)
                     {
                         Console.WriteLine(p);
                     }*/
                }
            }


            FTPConnection ftp = new FTPConnection();
            ftp.ConnectMode = FTPConnectMode.ACTIVE;
            ftp.ServerAddress = ConfigurationManager.AppSettings["ServerAddress"];
            ftp.UserName = ConfigurationManager.AppSettings["UserName"];
            ftp.Password = ConfigurationManager.AppSettings["UserName"];
            ftp.Connect();

            FTPFile[] filesFTP = ftp.GetFileInfos();
            if (filesFTP.Length > 0)
            {
                //int a = 0;
                for (int i = 0; i < Local_Short_listFiles.Count; i++)
                {
                    //Console.WriteLine(filesFTP[i].Name);
                    for (int k = 0; k < filesFTP.Length; k++)
                    {
                        if (filesFTP[k].Name == Local_Short_listFiles[i])
                        {
                            break;
                        }
                        if (k == filesFTP.Length - 1)
                        {
                            try
                            {
                                ftp.CreateDirectory(Local_Short_listFiles[i]);
                                main_Long_files = Directory.GetFiles(Local_long_listFiles[i]);
                                main_Short_files = Directory.GetFiles(Local_long_listFiles[i]);

                                for (int s = 0; s < main_Short_files.Length; s++)
                                {
                                    main_Short_files[s] = new FileInfo(main_Short_files[s]).Name;
                                    ftp.UploadFile(main_Long_files[s], ($"/{Local_Short_listFiles[i]}/{main_Short_files[s]}"), true);
                                }
                            }
                            catch (Exception a)
                            {
                                Console.WriteLine("Ошибка при создании папки и копировании файлов");
                            }
                        }
                    }
                    // удаляем каталог после записи
                    try
                    {
                        DirectoryInfo dirInfo = new DirectoryInfo(Local_long_listFiles[i]);
                        dirInfo.Delete(true);
                        Console.WriteLine("Каталог удален");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex.Message);
                    }
                }
            }
            else
            {
                for (int i = 0; i < Local_Short_listFiles.Count; i++)
                {
                    try
                    {
                        ftp.CreateDirectory(Local_Short_listFiles[i]);
                        main_Long_files = Directory.GetFiles(Local_long_listFiles[i]);
                        main_Short_files = Directory.GetFiles(Local_long_listFiles[i]);

                        for (int s = 0; s < main_Short_files.Length; s++)
                        {
                            main_Short_files[s] = new FileInfo(main_Short_files[s]).Name;
                            ftp.UploadFile(main_Long_files[s], ($"/{Local_Short_listFiles[i]}/{main_Short_files[s]}"), true);
                        }
                    }
                    catch (Exception a)
                    {
                        Console.WriteLine("Ошибка при создании папки и копировании файлов");
                    }
                    // удаляем каталог после записи
                    try
                    {
                        DirectoryInfo dirInfo = new DirectoryInfo(Local_long_listFiles[i]);
                        dirInfo.Delete(true);
                        Console.WriteLine("Каталог удален");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex.Message);
                    }
                }
            }

            // Получение списка директорий на сервере ////////////////////////////////////////////////////////////////////////////////////////////////
            FTPFile[] GUID_directory = ftp.GetFileInfos();  
            
            // сортировка директории по дате создания (первая самая новая)
            FTPFile temp;
            for (int i = 0; i < GUID_directory.Length - 1; i++)
            {
                for (int j = i + 1; j < GUID_directory.Length; j++)
                {
                    if (GUID_directory[i].LastModified < GUID_directory[j].LastModified)
                    {
                        temp = GUID_directory[i];
                        GUID_directory[i] = GUID_directory[j];
                        GUID_directory[j] = temp;
                    }
                }
            }

            Console.WriteLine("После сортировки");
            foreach (FTPFile a in GUID_directory)
            {
                Console.WriteLine(a.Name);
            }

            // Удаление файлов и директорий
            if (GUID_directory.Length > 1)
            {
                for (int i = 1; i < GUID_directory.Length; i++)
                {
                    // создаем папку на вычеслителе с именем партии
                    DirectoryInfo dirInfo = new DirectoryInfo(dir_Name + "/" + GUID_directory[i].Name);   
                    if (!dirInfo.Exists)
                    {
                        dirInfo.Create();
                    }
                    //dirInfo.CreateSubdirectory(subpath);


                    ftp.ChangeWorkingDirectory(GUID_directory[i].Name);     // сменить рабочую директорию, войти в папку партии
                    FTPFile[] Into_GUID = ftp.GetFileInfos();               // файлы в папке партии
                    for (int k = 0; k < Into_GUID.Length; k++)
                    {
                        if (Into_GUID[k].Dir)  // true если это директория
                        {
                            
                            dirInfo.CreateSubdirectory(Into_GUID[k].Name);             // создаем папку на вычеслителе с номером вагона
                            ftp.ChangeWorkingDirectory(Into_GUID[k].Name);             // сменить рабочую директорию, войти в папку номера вагона
                            FTPFile[] files = ftp.GetFileInfos();                      // файлы в папке вагона
                            foreach(FTPFile a in files)
                            {
                                // копирование файла с сервера на кампъютер
                                ftp.DownloadFile(a.Name, dir_Name + "/" + GUID_directory[i].Name + "/" + Into_GUID[k] + "/" + a.Name);
                            }
                            ftp.ChangeWorkingDirectoryUp();                    // вернуться из папки
                        }
                        else
                        {
                            ftp.DownloadFile(Into_GUID[k].Name, dir_Name + "/" + GUID_directory[i].Name + "/" + Into_GUID[k].Name);
                        }
                    }
                    //Console.WriteLine("Файлы удалены из директории");
                    ftp.ChangeWorkingDirectoryUp();                    // вернуться из папки
                    ftp.DeleteDirectory(GUID_directory[i].Name);            // удалить папку
                    Console.WriteLine("Старые директории удалены");

                }
                Console.WriteLine("Старые директории удалены");
            }

           
            ftp.Close();
            Console.Read();

        }
    }
}
