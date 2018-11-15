using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Forms;
using System.IO;
using System.Security.Cryptography;

namespace Encryption_App
{
    /// <summary>
    /// Interaction logic for FileEncryptor.xaml
    /// </summary>
    
    
    public partial class FileEncryptor : Window
    {
        //Crypto Objects declaration
        CspParameters cspp = new CspParameters();
        RSACryptoServiceProvider rsa;

        //Folder Locations
        const string EncrFolder = @"c:\Encrypt\";
        const string DecrFolder = @"c:\Decrypt\";
        const string SrcFolder = @"c:\docs\";

        // Public key file
        const string PubKeyFile = @"c:\encrypt\rsaPublicKey.txt";

        // Key container name for
        // private/public key value pair.
        const string keyName = "Key01";

        //Grab file ext
        public string ext = ""; 


        public FileEncryptor()
        {
            InitializeComponent();
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            //Store key in container
            cspp.KeyContainerName = keyName;
            rsa = new RSACryptoServiceProvider(cspp);
            rsa.PersistKeyInCsp = true;
            if (rsa.PublicOnly == true)
                label1.Content = "Key: " + cspp.KeyContainerName + " - Public Only";
            else
                label1.Content = "Key: " + cspp.KeyContainerName + " - Full Key Pair";

        }

        private void EncryptFile(string inFile)
        {

            // Rijndael call
            RijndaelManaged rjndl = new RijndaelManaged();
            rjndl.KeySize = 256;
            rjndl.BlockSize = 256;
            rjndl.Mode = CipherMode.CBC;
            ICryptoTransform transform = rjndl.CreateEncryptor();

            // Encrypt Rijndael key
            byte[] keyEncrypted = rsa.Encrypt(rjndl.Key, false);

            // Create byte arrays to contain the length values of the key and IV.
            byte[] LenK = new byte[4];
            byte[] LenIV = new byte[4];

            int lKey = keyEncrypted.Length;
            LenK = BitConverter.GetBytes(lKey);
            int lIV = rjndl.IV.Length;
            LenIV = BitConverter.GetBytes(lIV);

            // Write to the FileStream

            int startFileName = inFile.LastIndexOf("\\") + 1;
            string outFile = EncrFolder + inFile.Substring(startFileName, inFile.LastIndexOf(".") - startFileName) + ".enc";

            using (FileStream outFs = new FileStream(outFile, FileMode.Create))
            {

                outFs.Write(LenK, 0, 4);
                outFs.Write(LenIV, 0, 4);
                outFs.Write(keyEncrypted, 0, lKey);
                outFs.Write(rjndl.IV, 0, lIV);

                // Writes ciphertext
                using (CryptoStream outStreamEncrypted = new CryptoStream(outFs, transform, CryptoStreamMode.Write))
                {

                    // Chunk by chunk for efficiency
                    int count = 0;
                    int offset = 0;
                    int blockSizeBytes = rjndl.BlockSize / 8;
                    byte[] data = new byte[blockSizeBytes];
                    int bytesRead = 0;

                    using (FileStream inFs = new FileStream(inFile, FileMode.Open))
                    {
                        do
                        {
                            count = inFs.Read(data, 0, blockSizeBytes);
                            offset += count;
                            outStreamEncrypted.Write(data, 0, count);
                            bytesRead += blockSizeBytes;
                        }
                        while (count > 0);
                        inFs.Close();
                    }
                    outStreamEncrypted.FlushFinalBlock();
                    outStreamEncrypted.Close();
                }
                outFs.Close();
            }

        }

        private void DecryptFile(string inFile)
        {

            RijndaelManaged rjndl = new RijndaelManaged();
            rjndl.KeySize = 256;
            rjndl.BlockSize = 256;
            rjndl.Mode = CipherMode.CBC;

            //Byte array? -yes
            byte[] LenK = new byte[4];
            byte[] LenIV = new byte[4];

            // Recreate file (check how to check extension)
            string outFile = DecrFolder + inFile.Substring(0, inFile.LastIndexOf(".")) + ext;

            // Write as file is being read.
            using (FileStream inFs = new FileStream(EncrFolder + inFile, FileMode.Open))
            {

                inFs.Seek(0, SeekOrigin.Begin);
                inFs.Seek(0, SeekOrigin.Begin);
                inFs.Read(LenK, 0, 3);
                inFs.Seek(4, SeekOrigin.Begin);
                inFs.Read(LenIV, 0, 3);

                int lenK = BitConverter.ToInt32(LenK, 0);
                int lenIV = BitConverter.ToInt32(LenIV, 0);

                // Get ciphertext start pos. and length
                int startC = lenK + lenIV + 8;
                int lenC = (int)inFs.Length - startC;

                byte[] KeyEncrypted = new byte[lenK];
                byte[] IV = new byte[lenIV];

                // Get key
                inFs.Seek(8, SeekOrigin.Begin);
                inFs.Read(KeyEncrypted, 0, lenK);
                inFs.Seek(8 + lenK, SeekOrigin.Begin);
                inFs.Read(IV, 0, lenIV);
                Directory.CreateDirectory(DecrFolder);

                // Decrypt key (remove manual code and use .NET libraries
                byte[] KeyDecrypted = rsa.Decrypt(KeyEncrypted, false);
                ICryptoTransform transform = rjndl.CreateDecryptor(KeyDecrypted, IV);

                using (FileStream outFs = new FileStream(outFile, FileMode.Create))
                {

                    int count = 0;
                    int offset = 0;

                    int blockSizeBytes = rjndl.BlockSize / 8;
                    byte[] data = new byte[blockSizeBytes];

                    //Fails on large files
                    //e: works now (switched to chunking)
                    inFs.Seek(startC, SeekOrigin.Begin);
                    using (CryptoStream outStreamDecrypted = new CryptoStream(outFs, transform, CryptoStreamMode.Write))
                    {
                        do
                        {
                            count = inFs.Read(data, 0, blockSizeBytes);
                            offset += count;
                            outStreamDecrypted.Write(data, 0, count);

                        }
                        while (count > 0);

                        outStreamDecrypted.FlushFinalBlock();
                        outStreamDecrypted.Close();
                    }
                    outFs.Close();
                }
                inFs.Close();
            }

        }


        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            if (rsa == null)
                System.Windows.MessageBox.Show("Key not set. Please click 'Create Keys'.");
            else
            {

                // wpf dialog not working, using WinForms dialog check for now
                System.Windows.Forms.OpenFileDialog openFileDialog1 = new System.Windows.Forms.OpenFileDialog();
                openFileDialog1.InitialDirectory = SrcFolder;
                if (openFileDialog1.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    string fName = openFileDialog1.FileName;
                    if (fName != null)
                    {
                        FileInfo fInfo = new FileInfo(fName);
                        string name = fInfo.FullName;
                        ext = System.IO.Path.GetExtension(fName);
                        EncryptFile(name);
                        System.Windows.MessageBox.Show("The file has been successfully encrypted!");
                        System.Diagnostics.Process.Start(EncrFolder);
                    }
                }
            }
        }

        private void Button_Click_2(object sender, RoutedEventArgs e)
        {
            if (rsa == null)
                System.Windows.MessageBox.Show("Key not set. Click 'Create Keys'.");
            else
            {
                // Still need winforms for the result
                System.Windows.Forms.OpenFileDialog openFileDialog2 = new System.Windows.Forms.OpenFileDialog();
                openFileDialog2.InitialDirectory = EncrFolder;
                if (openFileDialog2.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    string fName = openFileDialog2.FileName;
                    if (fName != null)
                    {
                        FileInfo fi = new FileInfo(fName);
                        string name = fi.Name;
                        DecryptFile(name);
                        System.Windows.MessageBox.Show("The file has been successfully decrypted!");
                        System.Diagnostics.Process.Start(DecrFolder);
                    }
                }
            }
        }

        //IMPLEMENT THESE LINES AFTER BETTER RESEARCH!!!
    

        //private void Button_Click_3(object sender, RoutedEventArgs e)
        //{
        //    // Save the public key created by the RSA
        //    // to a file.
        //    Directory.CreateDirectory(EncrFolder);
        //    StreamWriter sw = new StreamWriter(PubKeyFile, false);
        //    sw.Write(rsa.ToXmlString(false));
        //    sw.Close();
        //}

        //private void Button_Click_4(object sender, RoutedEventArgs e)
        //{
        //    StreamReader sr = new StreamReader(PubKeyFile);
        //    cspp.KeyContainerName = keyName;
        //    rsa = new RSACryptoServiceProvider(cspp);
        //    string keytxt = sr.ReadToEnd();
        //    rsa.FromXmlString(keytxt);
        //    rsa.PersistKeyInCsp = true;
        //    if (rsa.PublicOnly == true)
        //        label1.Content = "Key: " + cspp.KeyContainerName + " - Public Only";
        //    else
        //        label1.Content = "Key: " + cspp.KeyContainerName + " - Full Key Pair";
        //    sr.Close();
        //}


        //private void Button_Click_5(object sender, RoutedEventArgs e)
        //{
        //    cspp.KeyContainerName = keyName;

        //    rsa = new RSACryptoServiceProvider(cspp);
        //    rsa.PersistKeyInCsp = true;

        //    if (rsa.PublicOnly == true)
        //        label1.Content = "Key: " + cspp.KeyContainerName + " - Public Only";
        //    else
        //        label1.Content = "Key: " + cspp.KeyContainerName + " - Full Key Pair";

        //}

        private void Button_Click_6(object sender, RoutedEventArgs e)
        {
            Launcher launcher = new Launcher();
            launcher.Show();
            this.Hide();
        }
    }
}
