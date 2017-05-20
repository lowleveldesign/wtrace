using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

namespace LowLevelDesign.WinTrace.Utilities
{
    class FileImageInMemory
    {
        private readonly ulong baseAddress;
        private readonly int imageSize;
        private readonly string fileName;

        public FileImageInMemory(string fileName, ulong baseAddress, int imageSize)
        {
            this.fileName = fileName;
            this.baseAddress = baseAddress;
            this.imageSize = imageSize;
        }

        public ulong BaseAddress { get { return baseAddress; } }

        public int ImageSize { get { return imageSize; } }

        public string FileName { get { return fileName; } }
    }

    static class DriverImageUtilities
    {
        private static readonly List<ulong> baseAddresses = new List<ulong>(200);
        private static readonly Dictionary<ulong, FileImageInMemory> loadedImages = new Dictionary<ulong, FileImageInMemory>(200);
        private static readonly ReaderWriterLockSlim lck = new ReaderWriterLockSlim();

        public static void AddImage(FileImageInMemory loadedImage)
        {
            lck.EnterWriteLock();
            try {
                int ind = baseAddresses.BinarySearch(loadedImage.BaseAddress);
                if (ind < 0) {
                    baseAddresses.Insert(~ind, loadedImage.BaseAddress);
                    loadedImages.Add(loadedImage.BaseAddress, loadedImage);
                } else {
                    Trace.TraceWarning("Problem when adding image data: 0x{0:X} - it is already added.", loadedImage.BaseAddress);
                }
            } finally {
                lck.ExitWriteLock();
            }
        }

        public static void RemoveImage(ulong baseAddress)
        {
            lck.EnterWriteLock();
            try {
                int ind = baseAddresses.BinarySearch(baseAddress);
                if (ind >= 0) {
                    baseAddresses.RemoveAt(ind);
                    loadedImages.Remove(baseAddress);
                } else {
                    Trace.TraceWarning("Problem when disposing image data: the image 0x{0:X} could not be found.", baseAddress);
                }
            } finally {
                lck.ExitWriteLock();
            }
        }

        public static FileImageInMemory FindImage(ulong address)
        {
            if (!lck.TryEnterReadLock(10)) {
                Trace.TraceWarning("Load image lock could not be obtained.");
                return null;
            }

            try {
                int ind = baseAddresses.BinarySearch(address);
                if (ind < 0) {
                    ind = ~ind;
                    // the bigger element can't be the first on the list
                    if (ind == 0) {
                        return null;
                    }
                    ind = ind - 1;
                }
                FileImageInMemory imageData;
                bool found = loadedImages.TryGetValue(baseAddresses[ind], out imageData);
                Debug.Assert(found);

                if ((int)(address - imageData.BaseAddress) > imageData.ImageSize) {
                    return null;
                }

                return imageData;
            } finally {
                lck.ExitReadLock();
            }
        }
    }
}
