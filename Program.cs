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
        public static string dir_Name;
        public static string serverAddress;
        public static string userName;
        public static string password;
        public static FTPConnection ftp;

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
                }
                ftp = new FTPConnection();
                ftp.ConnectMode = FTPConnectMode.ACTIVE;
                ftp.ServerAddress = serverAddress;
                ftp.UserName = userName;
                ftp.Password = password;
                ftp.Connect();
            }
            catch (Exception a)
            {
                log(a.ToString());
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
                Console.WriteLine(a.Name + " - " + a.LastModified);
            }
            // Копирование и удаление файлов и директорий ////////////////////////////////////////////////////////////////////////////////////////////
            if (GUID_directory.Length > 1)
            {
                for (int i = 1; i < GUID_directory.Length; i++)
                {
                    // создаем папку на вычислителе с именем партии
                    DirectoryInfo dirInfo = new DirectoryInfo(dir_Name + "/" + GUID_directory[i].Name);   
                    if (!dirInfo.Exists)
                    {
                        dirInfo.Create();
                    }
                    ftp.ChangeWorkingDirectory(GUID_directory[i].Name);     // сменить рабочую директорию, войти в папку партии
                    FTPFile[] Into_GUID = ftp.GetFileInfos();               // файлы в папке партии
                    for (int k = 0; k < Into_GUID.Length; k++)
                    {
                        if (Into_GUID[k].Dir)  // true если это директория
                        {
                            dirInfo.CreateSubdirectory(Into_GUID[k].Name);             // создаем папку на вычислителе с номером вагона
                            ftp.ChangeWorkingDirectory(Into_GUID[k].Name);
                            FTPFile[] files = ftp.GetFileInfos();                      // файлы в папке вагона
                            foreach(FTPFile a in files)
                            {
                                // копирование файла с сервера на компьютер
                                ftp.DownloadFile(dir_Name + "/" + GUID_directory[i].Name + "/" + Into_GUID[k].Name + "/" + a.Name, a.Name);
                                ftp.DeleteFile(a.Name);
                            }
                            ftp.ChangeWorkingDirectoryUp();                    // вернуться из папки
                            ftp.DeleteDirectory(Into_GUID[k].Name);
                        }
                        else
                        {
                            ftp.DownloadFile(dir_Name + "/" + GUID_directory[i].Name + "/" + Into_GUID[k].Name, Into_GUID[k].Name);
                            ftp.DeleteFile(Into_GUID[k].Name);
                        }
                    }
                    ftp.ChangeWorkingDirectoryUp();                    // вернуться из папки
                    ftp.DeleteDirectory(GUID_directory[i].Name);            // удалить папку
                    Console.WriteLine("Директория удалена");
                }
                Console.WriteLine("Старые директории удалены");
            }
            ftp.Close();
            Console.Read();
        }
    }
}
