using System;
using System.Collections.Generic;
using System.Linq;

using MediaFoundation;

using Primera.Image.RawFormats;

namespace CameraCapture.WPF.VideoCapture
{
    public delegate void VideoConversionDelegate(IntPtr pDest, int lDestStride, IntPtr pSrc, int lSrcStride, int dwWidthInPixels, int dwHeightInPixels);

    public static class UnmanagedImageConvert
    {
        public static List<VideoFormatGUID> VideoFormatDefs = new List<VideoFormatGUID> {
                new VideoFormatGUID(MFMediaType.RGB32, TransformImage_RGB32),
                new VideoFormatGUID(MFMediaType.RGB24, TransformImage_RGB24),
                new VideoFormatGUID(MFMediaType.YUY2, TransformImage_YUY2),
                new VideoFormatGUID(MFMediaType.NV12, TransformImage_NV12)
            };

        public static BGRA32 ConvertYCrCbToRGB(byte y, byte cr, byte cb)
        {
            BGRA32 rgbq = new BGRA32();

            int c = y - 16;
            int d = cb - 128;
            int e = cr - 128;

            rgbq.R = Clip((298 * c + 409 * e + 128) >> 8);
            rgbq.G = Clip((298 * c - 100 * d - 208 * e + 128) >> 8);
            rgbq.B = Clip((298 * c + 516 * d + 128) >> 8);
            rgbq.A = 0xff;

            return rgbq;
        }

        public static VideoConversionDelegate GetConversionFunction(Guid videoType)
        {
            return MatchFormat(videoType)?.VideoConvertFunction;
        }

        public static VideoFormatGUID GetFormat(int index)
        {
            if (index >= VideoFormatDefs.Count) return null;

            return VideoFormatDefs[index];
        }

        public static bool IsFormatSupported(Guid subtype)
        {
            return MatchFormat(subtype) is not null;
        }

        public static VideoFormatGUID MatchFormat(Guid videoType)
        {
            return VideoFormatDefs.FirstOrDefault(v => v.SubType == videoType) ?? throw new ArgumentOutOfRangeException($"No conversion function found for video type {videoType}");
        }

        //-------------------------------------------------------------------
        // TransformImage_NV12
        //
        // NV12 to RGB-32
        //-------------------------------------------------------------------
        public static unsafe void TransformImage_NV12(IntPtr pDest, int lDestStride, IntPtr pSrc, int lSrcStride, int dwWidthInPixels, int dwHeightInPixels)
        {
            Byte* lpBitsY = (byte*)pSrc;
            Byte* lpBitsCb = lpBitsY + (dwHeightInPixels * lSrcStride);
            Byte* lpBitsCr = lpBitsCb + 1;

            Byte* lpLineY1;
            Byte* lpLineY2;
            Byte* lpLineCr;
            Byte* lpLineCb;

            Byte* lpDibLine1 = (Byte*)pDest;
            for (UInt32 y = 0; y < dwHeightInPixels; y += 2)
            {
                lpLineY1 = lpBitsY;
                lpLineY2 = lpBitsY + lSrcStride;
                lpLineCr = lpBitsCr;
                lpLineCb = lpBitsCb;

                Byte* lpDibLine2 = lpDibLine1 + lDestStride;

                for (UInt32 x = 0; x < dwWidthInPixels; x += 2)
                {
                    byte y0 = lpLineY1[0];
                    byte y1 = lpLineY1[1];
                    byte y2 = lpLineY2[0];
                    byte y3 = lpLineY2[1];
                    byte cb = lpLineCb[0];
                    byte cr = lpLineCr[0];

                    BGRA32 r = ConvertYCrCbToRGB(y0, cr, cb);
                    lpDibLine1[0] = r.B;
                    lpDibLine1[1] = r.G;
                    lpDibLine1[2] = r.R;
                    lpDibLine1[3] = 0; // Alpha

                    r = ConvertYCrCbToRGB(y1, cr, cb);
                    lpDibLine1[4] = r.B;
                    lpDibLine1[5] = r.G;
                    lpDibLine1[6] = r.R;
                    lpDibLine1[7] = 0; // Alpha

                    r = ConvertYCrCbToRGB(y2, cr, cb);
                    lpDibLine2[0] = r.B;
                    lpDibLine2[1] = r.G;
                    lpDibLine2[2] = r.R;
                    lpDibLine2[3] = 0; // Alpha

                    r = ConvertYCrCbToRGB(y3, cr, cb);
                    lpDibLine2[4] = r.B;
                    lpDibLine2[5] = r.G;
                    lpDibLine2[6] = r.R;
                    lpDibLine2[7] = 0; // Alpha

                    lpLineY1 += 2;
                    lpLineY2 += 2;
                    lpLineCr += 2;
                    lpLineCb += 2;

                    lpDibLine1 += 8;
                    lpDibLine2 += 8;
                }

                pDest += (2 * lDestStride);
                lpBitsY += (2 * lSrcStride);
                lpBitsCr += lSrcStride;
                lpBitsCb += lSrcStride;
            }
        }

        //-------------------------------------------------------------------
        // TransformImage_RGB24
        //
        // RGB-24 to RGB-32
        //-------------------------------------------------------------------
        public static unsafe void TransformImage_RGB24(IntPtr pDest, int lDestStride, IntPtr pSrc, int lSrcStride, int dwWidthInPixels, int dwHeightInPixels)
        {
            BGR24* source = (BGR24*)pSrc;
            BGRA32* dest = (BGRA32*)pDest;

            lSrcStride /= 3;
            lDestStride /= 4;

            for (int y = 0; y < dwHeightInPixels; y++)
            {
                for (int x = 0; x < dwWidthInPixels; x++)
                {
                    dest[x].R = source[x].R;
                    dest[x].G = source[x].G;
                    dest[x].B = source[x].B;
                    dest[x].A = 0;
                }

                source += lSrcStride;
                dest += lDestStride;
            }
        }

        //-------------------------------------------------------------------
        // TransformImage_RGB32
        //
        // RGB-32 to RGB-32
        //
        // Note: This function is needed to copy the image from system
        // memory to the Direct3D surface.
        //-------------------------------------------------------------------
        public static void TransformImage_RGB32(IntPtr pDest, int lDestStride, IntPtr pSrc, int lSrcStride, int dwWidthInPixels, int dwHeightInPixels)
        {
            MediaFoundation.MFExtern.MFCopyImage(pDest, lDestStride, pSrc, lSrcStride, dwWidthInPixels * 4, dwHeightInPixels);
        }

        //-------------------------------------------------------------------
        // TransformImage_YUY2
        //
        // YUY2 to RGB-32
        //-------------------------------------------------------------------
        public static unsafe void TransformImage_YUY2(
            IntPtr pDest,
            int lDestStride,
            IntPtr pSrc,
            int lSrcStride,
            int dwWidthInPixels,
            int dwHeightInPixels)
        {
            YUYV* pSrcPel = (YUYV*)pSrc;
            BGRA32* pDestPel = (BGRA32*)pDest;

            lSrcStride /= 4; // convert lSrcStride to YUYV
            lDestStride /= 4; // convert lDestStride to RGBQUAD

            for (int y = 0; y < dwHeightInPixels; y++)
            {
                for (int x = 0; x < dwWidthInPixels / 2; x++)
                {
                    pDestPel[x * 2] = ConvertYCrCbToRGB(pSrcPel[x].Y, pSrcPel[x].V, pSrcPel[x].U);
                    pDestPel[(x * 2) + 1] = ConvertYCrCbToRGB(pSrcPel[x].Y2, pSrcPel[x].V, pSrcPel[x].U);
                }

                pSrcPel += lSrcStride;
                pDestPel += lDestStride;
            }
        }

        private static byte Clip(int clr)
        {
            return (byte)(clr < 0 ? 0 : (clr > 255 ? 255 : clr));
        }
    }
}