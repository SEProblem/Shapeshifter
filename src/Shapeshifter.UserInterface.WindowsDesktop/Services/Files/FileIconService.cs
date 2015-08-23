﻿using System;
using System.Runtime.InteropServices;
using Shapeshifter.UserInterface.WindowsDesktop.Services.Interfaces;
using static Shapeshifter.UserInterface.WindowsDesktop.Services.Api.IconApi;
using System.Windows.Interop;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Diagnostics.CodeAnalysis;

namespace Shapeshifter.UserInterface.WindowsDesktop.Services
{
    [ExcludeFromCodeCoverage]
    class FileIconService : IFileIconService
    {
        readonly IImagePersistenceService imagePersistenceService;

        public FileIconService(IImagePersistenceService imagePersistenceService)
        {
            this.imagePersistenceService = imagePersistenceService;
        }

        public byte[] GetIcon(string path, bool allowThumbnails, int dimensions = 256)
        {
            var bitmapHandle = GenerateBitmapHandle(path, allowThumbnails, dimensions);
            if (bitmapHandle != IntPtr.Zero)
            {
                return GenerateByteArrayFromBitmapHandle(bitmapHandle);
            }
            else
            {
                throw new InvalidOperationException("Could not fetch an icon from the given path.");
            }
        }

        byte[] GenerateByteArrayFromBitmapHandle(IntPtr bitmapHandle)
        {
            var bitmap = new BITMAP();
            AllocateBitmapSpace(bitmapHandle, ref bitmap);

            try
            {
                FillBitmapBitsIntoHandle(bitmap);

                var bitmapSource = CreateBitmapSourceFromHandle(bitmapHandle, bitmap);
                return imagePersistenceService.ConvertBitmapSourceToByteArray(bitmapSource);
            }
            finally
            {
                DeleteObject(bitmapHandle);
            }
        }

        static void FillBitmapBitsIntoHandle(BITMAP bitmap)
        {
            var bytes = new byte[bitmap.WidthBytes * bitmap.Height];
            Marshal.Copy(bitmap.Bits, bytes, 0, bytes.Length);
        }

        static BitmapSource CreateBitmapSourceFromHandle(IntPtr bitmapHandle, BITMAP bitmap)
        {
            var bitmapSource = Imaging.CreateBitmapSourceFromHBitmap(bitmapHandle, IntPtr.Zero, new Int32Rect(0, 0, bitmap.Width, bitmap.Height), BitmapSizeOptions.FromWidthAndHeight(bitmap.Width, bitmap.Height));
            bitmapSource.Freeze();
            return bitmapSource;
        }

        static void AllocateBitmapSpace(IntPtr bitmapHandle, ref BITMAP bitmap)
        {
            var bufferSize = Marshal.SizeOf(bitmap);
            GetObject(bitmapHandle, bufferSize, out bitmap);
        }

        static IntPtr GenerateBitmapHandle(string filePath, bool allowThumbnails, int dimensions)
        {
            try
            {
                return GetFactoryImage(dimensions, filePath, allowThumbnails);
            }
            catch (COMException ex) when (ex.ErrorCode == -2147175936 && allowThumbnails)
            {
                return GetFactoryImage(dimensions, filePath, false);
            }
        }

        static IntPtr GetFactoryImage(int dimensions, string path, bool allowThumbnails)
        {
            var factory = GenerateShellItemImageFactory(path);

            var bitmapHandle = IntPtr.Zero;

            var imageScope = GenerateScopeFromThumbnailInformation(allowThumbnails);
            factory.GetImage(new SIZE(dimensions, dimensions), imageScope, ref bitmapHandle);

            return bitmapHandle;
        }

        static IShellItemImageFactory GenerateShellItemImageFactory(string path)
        {
            var uniqueId = new Guid("43826d1e-e718-42ee-bc55-a1e261c37bfe");

            IShellItem rawFactory;
            SHCreateItemFromParsingName(path, IntPtr.Zero, uniqueId, out rawFactory);

            var factory = (IShellItemImageFactory)rawFactory;
            return factory;
        }

        static SIIGBF GenerateScopeFromThumbnailInformation(bool allowThumbnails)
        {
            var type = allowThumbnails ?
                SIIGBF.SIIGBF_THUMBNAILONLY :
                SIIGBF.SIIGBF_ICONONLY;
            return type | SIIGBF.SIIGBF_BIGGERSIZEOK;
        }
    }
}