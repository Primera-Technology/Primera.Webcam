using System;
using System.Linq;
using System.Text;

namespace Primera.ImageTransform.RawFormats
{
    /// <summary>
    /// Four CC codes are used to identify media types, especially in Media Foundation.
    /// See this document by Microsoft for details: https://learn.microsoft.com/en-us/windows/win32/directshow/fourcc-codes
    /// </summary>
    public static class FourCCHelper
    {
        /// <summary>
        /// Given a <see cref="Guid"/> that represents a subtype of a general media type,
        /// identify the FourCC media type using the given subtype.
        /// </summary>
        /// <param name="guid">The subtype GUID for a specific media type</param>
        /// <returns>A FourCC string like "YUY2" to be used for further processing</returns>
        public static string GetFourccTag(Guid guid)
        {
            byte[] fourccBytes = guid.ToByteArray()
                .Take(4)    // The first 4 bytes of a GUID are the 4CC code
                .Reverse()  // FourCC codes are always reversed for endianness
                .ToArray();

            // The FourCC is simply in ASCII at this point
            return Encoding.ASCII.GetString(fourccBytes);
        }
    }
}