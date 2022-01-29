using MahApps.Metro.Controls;
using Microsoft.Toolkit.Uwp.Notifications;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;

namespace SSRMT3D
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : MetroWindow
    {

        static string inFolderDir = "";    // Input Directory - Assumes PngBitmap
        static string outFolderDir = "";   // Output Directory
        static int fileCount = 0;
        static string[] inFiles;

        // Large Variables
        public static byte[,,] imageStack;
        public static List<BitmapSource> outputStack = new List<BitmapSource>();

        // Algorithm Variables
        public static int stride;

        public MainWindow() {
            InitializeComponent();
        }

        /**************** UI Functions ****************/
        public void updateText(string text) {
            txtStatusBlock.AppendText(text + Environment.NewLine);
            txtStatusBlock.Focus();
            txtStatusBlock.CaretIndex = txtStatusBlock.Text.Length;
            txtStatusBlock.ScrollToEnd();
        }


        /**************** OnClickListeners ****************/
        private void LaunchGitHubSite(object sender, RoutedEventArgs e) {
            // TODO - Reference back to Github
        }

        // Select Image In Folder Directory. // TODO - Create multiple-directory system
        private void btnInDir_Click(object sender, RoutedEventArgs e) {
            Ookii.Dialogs.Wpf.VistaFolderBrowserDialog folderBrowserDialog = new Ookii.Dialogs.Wpf.VistaFolderBrowserDialog();
            if (folderBrowserDialog.ShowDialog() == true) {
                inFolderDir = folderBrowserDialog.SelectedPath;
                txtInFolder.Text = inFolderDir;
                // Get File Count, Store File Names to 'inFiles'
                //var filters = new String[] { "jpg", "jpeg", "png", "gif", "tiff", "bmp", "svg" };
                var filters = new String[] {"png"};
                inFiles = Helpers.GetFilesFrom(inFolderDir, filters, false);
                fileCount = inFiles.Length;
                txtFileCount.Text = fileCount.ToString();
                updateText("Valid Input Directory Selected - " + fileCount + " Files Found");
            }
            // Validate Run Button
            ValidateRunButton();
        }

        // Select Image Out Folder Directory
        private void btnOutDir_Click(object sender, RoutedEventArgs e) {
            Ookii.Dialogs.Wpf.VistaFolderBrowserDialog folderBrowserDialog = new Ookii.Dialogs.Wpf.VistaFolderBrowserDialog();
            if (folderBrowserDialog.ShowDialog() == true) {
                outFolderDir = folderBrowserDialog.SelectedPath;
                txtOutFolder.Text = outFolderDir;
                updateText("Valid Output Directory Selected");
            }
            // Validate Run Button
            ValidateRunButton();
        }

        private void ValidateRunButton() {
            if ((fileCount != 0) && (outFolderDir.Length != 0)) {
                btnRun.IsEnabled = true;
            }
        }

        private async void btnRun_Click(object sender, RoutedEventArgs e) {
            btnRun.IsEnabled = false;

            ReadWrite readWrite = new ReadWrite();

            // Read in Images
            int readCount = readWrite.ReadImageStack(inFolderDir, inFiles, inFiles.Length, 0, inFiles.Length);
            updateText("Files converted to stack - Count: " + readCount);

            // Set Progress Bar
            pgrsBar.Value = 0;

            // Run Superpixel Algorithm
            Superpixel3D superPixel3D = new Superpixel3D();
            await Task.Run(() => superPixel3D.SLIC(2048, 50, readWrite.getX(), readWrite.getY(), readCount));

            // Iterate SLIC
            for (int i = 0; i < 10; i++) {
                await Task.Run(() => superPixel3D.iterate());
                pgrsBar.Value += 10;
                updateText("SLIC Iteration " + (i + 1));
            }

            // Output SLIC
            int writeCount = await Task.Run(() => superPixel3D.output(outFolderDir));

            updateText("Algorithm Complete: " + writeCount + " files written to output directory");

            // Toast Notification to alert user
            new ToastContentBuilder()
                .AddArgument("conversationId", 9813)
                .AddText("SRM Complete!")
                .Show();

            // Reset Button
            pgrsBar.Value = 100;
            btnRun.IsEnabled = true;
        }
    }
}

