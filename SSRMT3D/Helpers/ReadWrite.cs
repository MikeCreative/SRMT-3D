using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Windows.Media.Imaging;

namespace SSRMT3D
{
    public class ReadWrite
    {

        public int x;
        public int y;
        public int z;

        public int ReadImageStack(string dir, string[] files, int z, int start, int end) {
            // Get Image Properties (x, y, z). Get First Image, assumes all images are the width and height.
            Stream imageStreamSource = new FileStream(files[0], FileMode.Open, FileAccess.Read, FileShare.Read);
            PngBitmapDecoder decoder = new PngBitmapDecoder(imageStreamSource, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.Default);
            x = decoder.Frames[0].PixelWidth;
            y = decoder.Frames[0].PixelHeight;
            this.z = z;

            // Initialise ImageStack
            MainWindow.imageStack = new byte[x, y, z];

            // Read in all Files and store them in the ImageStack
            int i;
            for (i = start; i < end; i++) {      // Scale images? This takes around 4-5 GB for 500 images
                imageStreamSource = new FileStream(files[i], FileMode.Open, FileAccess.Read, FileShare.Read);
                decoder = new PngBitmapDecoder(imageStreamSource, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.Default);
                byte[] bitmapArray = Helpers.BitmapSourceToArray(decoder.Frames[0]);

                for (int ix = 0; ix < x; ix++) {
                    for (int iy = 0; iy < y; iy++) {
                        MainWindow.imageStack[ix, iy, i - start] = bitmapArray[ix + iy * x];
                    }
                }

                // Get Stride
                MainWindow.stride = decoder.Frames[0].PixelWidth * (decoder.Frames[0].Format.BitsPerPixel / 8);// TODO - Optimise, don't do this 1000+ times
            }
            return i - start;   // Return Files Read
        }

        public int WriteImageStack(string dir, int z) {
            int i;
            for (i = 0; i < z; i++) {      // Scale images? This takes around 4-5 GB for 500 images
                using (var fileStream = new FileStream(dir + "/" + "srmOut" + i + ".bmp", FileMode.Create)) {
                    BitmapEncoder encoder = new PngBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(MainWindow.outputStack[i]));
                    encoder.Save(fileStream);
                }
            }
            return i;   // Return Files Written
        }

        public int getX() {
            return x;
        }

        public int getY() {
            return y;
        }

    }
}
