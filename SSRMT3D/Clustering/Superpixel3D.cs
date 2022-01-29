using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Media;
using System.Windows.Media.Imaging;

/// <summary>
/// Superpixel Clustering for 3D Image stacks
/// Michael Howes - For Flinders University
/// 
/// References
/// Based on Matlabs Superpixels3 Algorithm https://au.mathworks.com/help/images/ref/superpixels3.html#bvczrh_-6
/// Python Example https://github.com/darshitajain/SLIC  https://darshita1405.medium.com/superpixels-and-slic-6b2d8a6e4f08
/// C# Example https://github.com/kruherson1337/SLIC/blob/master/SLIC/ImageProcessing.cs
/// 
/// Basic SLIC Algorithm Outline
/// 4 dimensional space [gxyz] where g is the grey level, and x,y,z is the pixel position.
/// N is Number of pixels in input image
/// K is Number of Superpixels used to segment the input image
/// N/K is Approximate size of each superpixel
/// S = sqrt(N/K), Grid interval for roughly equally sized superpixels
/// 
/// Ck = [gk, xk, yk, zk], k = [1,K]. 
/// Spacial extent of any superpixel is approximately S^2, safe to assume pixels associated with this cluster lie within 2S x 2S boundary.
/// 
/// Ds = dlab + (m/S) * dxy
/// Where dlab = distance of Ck to pixel in grey space.
/// Where dxy = distance of Ck to pixel in x,y,z space.
/// m = control of compactness
/// 
/// G(x,y) = (See Darshita)
/// 
/// Warning - For large 3D datasets, expect high RAM usage. 
/// 
/// Update - Use CUDA for increased operation speed https://surban.github.io/managedCuda/
/// 
/// </summary>

namespace SSRMT3D
{
    class Superpixel3D
    {

        List<SuperPixel> clusters = new List<SuperPixel>();     // Superpixels
        private long N;     // Number of Pixels in imageStack
        private int K;      // Number of Superpixels

        private int Sxy;    // Superpixel Grid Interval on x/y plane
        private int Sz;     // Superpixel Grid Interval in z axis

        private int m;      // Control Variable

        // Image Stack x,y,z size
        private int x;
        private int y;
        private int z;

        private float[,,] dis;      // Distance Array
        private int[,,] tag;        // Array to tag Pixels to clusters

        // Function to initialise the cluster centres, equally spaced within the 3D space
        public void initClusters() {
            clusters.Clear();           // Clear Clusters Array

            int x_offset = Sxy / 2;
            int y_offset = Sxy / 2;
            int z_offset = Sz / 2;

            while (x_offset < x) {
                while (y_offset < y) {
                    while (z_offset < z) {
                        clusters.Add(new SuperPixel(MainWindow.imageStack[x_offset, y_offset, z_offset], x_offset, y_offset, z_offset));  // Grey not required
                        z_offset += Sz; // Increment Z
                    }
                    z_offset = Sz / 2;  // Reset Z to First Row
                    y_offset += Sxy;    // Increment Y
                }
                z_offset = Sz / 2;      // Reset Z to First Row
                y_offset = Sxy / 2;     // Reset Y to first Column
                x_offset += Sxy;        // Increment X
            }
        }

        // Calculate the gradient at each pixel - Returns Greyscale gradient
        public double calcGradient(int pixel_x, int pixel_y, int pixel_z) {
            if ((pixel_x + 1) >= x) { pixel_x = x - 2; }        // Check OOB
            if (pixel_x < 0) { pixel_x = 0; }                   // Prevent edge pixels moving OOB
            if ((pixel_y + 1) >= y) { pixel_y = y - 2; }        // Check OOB
            if (pixel_y < 0) { pixel_y = 0; }                   // Prevent edge pixels moving OOB
            if ((pixel_z + 1) >= z) { pixel_z = z - 2; }        // Check OOB
            if (pixel_z < 0) { pixel_z = 0; }                   // Prevent edge pixels moving OOB
            return MainWindow.imageStack[pixel_x + 1, pixel_y + 1, pixel_z + 1] - MainWindow.imageStack[pixel_x, pixel_y, pixel_z];
        }

        // Reassign the cluster center to the pixel having the lowest gradient
        public void reassignClusterCenterToGradient() {
            for (int i = 0; i < clusters.Count; i++) {
                double cluster_gradient = calcGradient(clusters[i].x, clusters[i].y, clusters[i].z);        // Calculate cluster gradient
                for (int dx = -1; dx <= 1; dx++) {
                    for (int dy = -1; dy <= 1; dy++) {
                        for (int dz = -1; dz <= 1; dz++) {
                            int X = clusters[i].x + dx;
                            int Y = clusters[i].y + dy;
                            int Z = clusters[i].z + dz;

                            double new_gradient = calcGradient(X, Y, Z);
                            if (new_gradient < cluster_gradient) {      // If new gradient value is less than cluster gradient, Update Clusters
                                clusters[i].x = X;
                                clusters[i].y = Y;
                                clusters[i].z = Z;
                                cluster_gradient = new_gradient;
                            }
                        }
                    }
                }
            }
        }

        // Assign the pixels to the nearest cluster using Euclidean distance invololving grey (colour) and spacial proximity
        public void assignPixelsToCluster() {
            double dist_x = 0; double dist_y = 0;
            for (int i = 0; i < clusters.Count; i++) {
                for (int dx = (clusters[i].x - Sxy); dx < (clusters[i].x + Sxy); dx++) {
                    if ((dx < 0) || dx >= x) { continue; }
                    dist_x = (dx - clusters[i].x) * (dx - clusters[i].x);       // Pre-calculate 
                    for (int dy = (clusters[i].y - Sxy); dy < (clusters[i].y + Sxy); dy++) {
                        if ((dy < 0) || dy >= y) { continue; }
                        dist_y = (dy - clusters[i].y) * (dy - clusters[i].y);   // Pre-calculate
                        for (int dz = (clusters[i].z - Sz); dz < (clusters[i].z + Sz); dz++) {
                            if ((dz < 0) || dz >= z) { continue; }
                            byte grey = MainWindow.imageStack[dx, dy, dz];
                            double Dc = 0;
                            // Find Absolute Value
                            if (grey > clusters[i].grey) {
                                Dc = (grey - clusters[i].grey);
                            }
                            else {
                                Dc = (clusters[i].grey - grey);
                            }
                            //double Dc = Math.Abs(grey - clusters[i].grey);    // Channel Level Difference (Avoids sqrt(^2) as it is only 1D)
                            double Ds_Power = dist_x + dist_y + (dz - clusters[i].z) * (dz - clusters[i].z);
                            double Ds = Math.Sqrt(Ds_Power); // Euclidean Distance - Test without sqrt
                            double D = (Dc / m) * (Dc / m) + (Ds / Sxy) * (Ds / Sxy);
                            if (D < dis[dx, dy, dz]) {
                                tag[dx, dy, dz] = i;
                                dis[dx, dy, dz] = (float)D;
                            }
                        }
                    }
                }
            }
        }

        // Replace the Cluster Center with the mean of the pixels contained in the cluster
        public void updateClusterMean() {
            long[,] sum = new long[3, clusters.Count];      // Variable to hold x,y,z positions of pixels
            int[] count = new int[clusters.Count];          // Variable to number of pixels in cluster
            // Iterate through all pixels in cluster
            for (int xi = 0; xi < x; xi++) {
                for (int yi = 0; yi < y; yi++) {
                    for (int zi = 0; zi < z; zi++) {
                        if (tag[xi, yi, zi] > count.Length) { continue; }
                        sum[0, tag[xi, yi, zi]] += xi;
                        sum[1, tag[xi, yi, zi]] += yi;
                        sum[2, tag[xi, yi, zi]] += zi;
                        count[tag[xi, yi, zi]] += 1;
                    }
                }
            }

            // Divide sum value above by count to get average
            for (int i = 0; i < clusters.Count; i++) {
                if (count[i] == 0) { continue; }
                clusters[i].x = (int)(sum[0, i] / count[i]);
                clusters[i].y = (int)(sum[1, i] / count[i]);
                clusters[i].z = (int)(sum[2, i] / count[i]);
                clusters[i].grey = MainWindow.imageStack[clusters[i].x, clusters[i].y, clusters[i].z];
            }
        }

        // Repalce the colour of each pixel in a cluster by the colour of the cluster's center
        public void averageColourCluster() {
            for (int xi = 0; xi < x; xi++) {
                for (int yi = 0; yi < y; yi++) {
                    for (int zi = 0; zi < z; zi++) {
                        if (tag[xi, yi, zi] > clusters.Count) { continue; }
                        MainWindow.imageStack[xi, yi, zi] = clusters[tag[xi, yi, zi]].grey;        // Debug clusters[tag[xi,yi,zi]].grey
                    }
                }
            }
        }

        // Main Entrypoint for SLIC Algorithm
        public void SLIC(int K, int m, int x, int y, int z) {
            this.x = x;
            this.y = y;
            this.z = z;

            this.m = m;
            this.K = K;

            N = x * y * z;                      // Total Number of Pixels
            int S = (int)Math.Cbrt(K);         // Calculate Superpixels per axis
            Sxy = x / S;
            Sz = z / S;

            // Initialise Distance Array - Optimise
            dis = new float[x, y, z];
            tag = new int[x, y, z];
            for (int i = 0; i < x; i++) {
                for (int j = 0; j < y; j++) {
                    for (int k = 0; k < z; k++) {
                        dis[i, j, k] = float.MaxValue;
                        tag[i, j, k] = int.MaxValue;
                    }
                }
            }

            // Initialise Clusters
            initClusters();

            // Reassign_cluster_center
            reassignClusterCenterToGradient();
        }

        public void iterate() {
            assignPixelsToCluster();
            updateClusterMean();
        }

        public int output(string outFolderDir) {
            // Set pixels to superpixel values
            averageColourCluster();

            MainWindow.outputStack.Clear();
            for (int i = 0; i < z; i++) {
                byte[] p = new byte[x * y];
                for (int j = 0; j < x; j++) {
                    for (int k = 0; k < y; k++) {
                        p[j + k * x] = MainWindow.imageStack[j, k, i];
                    }
                }
                //Buffer.BlockCopy(MainWindow.imageStack, x * y * i, p, 0, p.Length); // Does not work as expected, use above method
                BitmapSource outputImage = BitmapSource.Create(x, y, 96, 96, PixelFormats.Gray8, null, p, MainWindow.stride);
                MainWindow.outputStack.Add(outputImage);
            }

            ReadWrite readWrite = new ReadWrite();
            return readWrite.WriteImageStack(outFolderDir, z);
        }
    }

    // Class to hold the superpixel clusters
    internal class SuperPixel
    {
        public byte grey = 0;   // Also the 'Index'
        public int x = 0;       // x position 
        public int y = 0;       // y position
        public int z = 0;       // z position

        public SuperPixel(byte grey, int x, int y, int z) {
            this.grey = grey;
            this.x = x;
            this.y = y;
            this.z = z;
        }
    }
}
