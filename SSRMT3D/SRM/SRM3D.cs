using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Media;
using System.Windows.Media.Imaging;

/// <summary>
/// SRM for 3D images, based on superpixel regions
/// Michael Howes - For Flinders University
/// 
/// References
/// Based on Java implementation of SRM https://github.com/fiji/Statistical_Region_Merging
/// 
/// </summary>

namespace SSRMT3D
{
    class SRM3D
    {
        float g = 256;          // number of different intensity values
        protected float Q = 25; // complexity of the assumed distributions
        protected float delta;
        int clusterSize = 0;   // Number of Clusters created from the Superpixel3D algorithm

        // Image Stack x,y,z size
        private int x;
        private int y;
        private int z;

        private Superpixel3D superpixel3D;
        protected float factor, logDelta;

        /*
		 * For performance reasons, these are held in "clusterSize" sized arrays
		 */
        float[] average;
        int[] count;
        int[] regionIndex; // if < 0, it is -1 - actual_regionIndex

        List<int[]> nextNeighbour = new List<int[]>();
        List<List<int>> sortedNeighbour;

        // Main Entry for SRM3D.
        public void Srm3D(Superpixel3D superpixel, int Q, int x, int y, int z, bool showAverages) {
            this.x = x;
            this.y = y;
            this.z = z;
            clusterSize = superpixel.getClusterCount(); // Max number of superpixels used in the Superpixel3D algorithm. Reduces SRM bucketsize.
            superpixel3D = superpixel;

            this.Q = Q;

            delta = 1f / (6 * x * y * z);
            /*
			 * This would be the non-relaxed formula:
			 *
			 * factor = g * g / 2 / Q * (float)Math.log(2 / delta);
			 *
			 * The paper claims that this is more prone to oversegmenting.
			 */
            factor = g * g / 2 / Q;
            logDelta = 2f * (float)Math.Log(6 * x * y * z);


            initializeRegions3D(x, y, z);
            initializeNeighbors3D(x, y, z);
            mergeAllNeighbors3D(x, y);

            int regionCount = consolidateRegions();
            Console.WriteLine("Regions: " + regionCount);
            superpixel3D.updateTagbyRegions(regionIndex);

        }

        // Function to initalise the 3D regions
        void initializeRegions3D(int w, int h, int d) {
            average = new float[clusterSize];
            count = new int[clusterSize];
            regionIndex = new int[clusterSize];


            for (int i = 0; i < clusterSize; i++) {
                average[i] = superpixel3D.getSuperPixel(i).grey;
                regionIndex[i] = i;
            }

            // Count
            for (int i = 0; i < w; i++) {
                for (int j = 0; j < h; j++) {
                    for (int k = 0; k < d; k++) {
                        int tag = superpixel3D.getTag(i, j, k);
                        if (tag < count.Length) {
                            count[tag]++;
                        } else {
                            Console.WriteLine();
                        }
                    }
                }
            }
        }

        // Function to add Neighbour Pair to list
        protected void addNeighborPair(int nextNeighbourIndex, int index1, int index2) {
            if (index1 >= superpixel3D.getClusterCount()) { return; }       // Prevent OOB
            if (index2 >= superpixel3D.getClusterCount()) { return; }       // Prevent OOB
            float diff = Math.Abs((float)superpixel3D.getSuperPixel(index1).grey - (float) superpixel3D.getSuperPixel(index2).grey);
            // Optimise Sorting Algorithm
            if (index1 <= index2) {
                int[] neighbourInfo = { index1, index2, (int)diff };
                nextNeighbour.Add(neighbourInfo);
            } else {
                int[] neighbourInfo = { index2, index1, (int)diff };
                nextNeighbour.Add(neighbourInfo);
            }
        }

        // This function iteraties through all pixels and adds pixels which have neighbours in different superpixels
        void initializeNeighbors3D(int w, int h, int d) {
            // For all pixels, check +1x, +1y, +1z to see if there is a neighbour
            for (int i = 0; i < w - 1; i++) {
                for (int j = 0; j < h - 1; j++) {
                    for (int k = 0; k < d - 1; k++) {
                        int index = i + (j * w) + (k * h);
                        if (superpixel3D.getTag(i, j, k) != superpixel3D.getTag(i + 1, j, k)) {
                            addNeighborPair(index, superpixel3D.getTag(i, j, k), superpixel3D.getTag(i + 1, j, k));
                        }

                        if (superpixel3D.getTag(i, j, k) != superpixel3D.getTag(i, j + 1, k)) {
                            addNeighborPair(index, superpixel3D.getTag(i, j, k), superpixel3D.getTag(i, j + 1, k));
                        }

                        if (superpixel3D.getTag(i, j, k) != superpixel3D.getTag(i, j, k + 1)) {
                            addNeighborPair(index, superpixel3D.getTag(i, j, k), superpixel3D.getTag(i, j, k + 1));
                        }
                    }
                }
            }

            // Remove all Duplicates and Empty Data
            var uniqueLst = nextNeighbour.GroupBy(c => String.Join(",", c)).Select(c => c.First().ToList()).ToList();

            // Clear nextNeighbour
            nextNeighbour.Clear();

            // Sort by 3rd value [Diff]
            sortedNeighbour = uniqueLst.OrderBy(x => x[2]).ToList();
            uniqueLst.Clear();
        }

        // recursively find out the region index for this pixel
        int getRegionIndex(int i) {
            i = regionIndex[i];
            while (i < 0)
                i = regionIndex[-1 - i];
            return i;
        }

        // Predicate to merge region i1 and i2
        bool predicate(int i1, int i2) {
            float difference = average[i1] - average[i2];
            float log1 = (float)Math.Log(1 + count[i1]) * (g < count[i1] ? g : count[i1]);
            float log2 = (float)Math.Log(1 + count[i2]) * (g < count[i2] ? g : count[i2]);
            return difference * difference < .1f * factor * ((log1 + logDelta) / count[i1] + ((log2 + logDelta) / count[i2]));
        }

        // Main Loop function for SRM. Iterates through all neighbours and merges if the predicate returns true
        void mergeAllNeighbors3D(int w, int h) {
            int I1 = 0;
            int I2 = 0;
            for (int i = 0; i < sortedNeighbour.Count; i++) {
                I1 = sortedNeighbour[i][0];
                I2 = sortedNeighbour[i][1];

                I1 = getRegionIndex(I1);
                I2 = getRegionIndex(I2);

                if (predicate(I1, I2)) {
                    mergeRegions(I1, I2);
                }
            }
        }

        // Function to merge two regions together
        void mergeRegions(int i1, int i2) {
            if (i1 != i2) {
                int mergedCount = count[i1] + count[i2];
                float mergedAverage = (average[i1] * count[i1] + average[i2] * count[i2]) / mergedCount;

                // merge larger index into smaller index
                if (i1 > i2) {
                    average[i2] = mergedAverage;
                    count[i2] = mergedCount;
                    regionIndex[i1] = -1 - i2;
                }
                else {
                    average[i1] = mergedAverage;
                    count[i1] = mergedCount;
                    regionIndex[i2] = -1 - i1;
                }
            }
        }


        int consolidateRegions() {
            /*
			 * By construction, a negative regionIndex will always point
			 * to a smaller regionIndex.
			 *
			 * So we can get away by iterating from small to large and
			 * replacing the positive ones with running numbers, and the
			 * negative ones by the ones they are pointing to (that are
			 * now guaranteed to contain a non-negative index).
			 */
            int count = 0;
            for (int i = 0; i < regionIndex.Length; i++)
                if (regionIndex[i] < 0)
                    regionIndex[i] =
                        regionIndex[-1 - regionIndex[i]];
                else
                    regionIndex[i] = count++;
            return count;
        }

        // Output Images
        public int output(string outFolderDir, bool printToFolde) {
            MainWindow.outputStack.Clear();
            for (int i = 0; i < z; i++) {
                byte[] p = new byte[x * y];
                for (int j = 0; j < x; j++) {
                    for (int k = 0; k < y; k++) {
                        p[j + k * x] = MainWindow.imageStack[j, k, i];
                    }
                }
                BitmapSource outputImage = BitmapSource.Create(x, y, 96, 96, PixelFormats.Gray8, null, p, MainWindow.stride);
                MainWindow.outputStack.Add(outputImage);
            }

            ReadWrite readWrite = new ReadWrite();
            return readWrite.WriteImageStack(outFolderDir, z);
        }
    }
}
