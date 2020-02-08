using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

namespace MigracaoCIPA
{
    static class ImageHelper
    {
        public static string SalvarImagemJPEG(string fotoOrigem, string fotoDestino, long qualidade)
        {
            Bitmap myBitmap;
            ImageCodecInfo myImageCodecInfo;
            System.Drawing.Imaging.Encoder myEncoder;
            EncoderParameter myEncoderParameter;
            EncoderParameters myEncoderParameters;

            // Create a Bitmap object based on a BMP file.
            using (myBitmap = new Bitmap(fotoOrigem))
            {
                // Get an ImageCodecInfo object that represents the JPEG codec.
                myImageCodecInfo = GetEncoderInfo("image/jpeg");

                // Create an Encoder object based on the GUID
                // for the Quality parameter category.
                myEncoder = System.Drawing.Imaging.Encoder.Quality;

                // Create an EncoderParameters object.
                // An EncoderParameters object has an array of EncoderParameter
                // objects. In this case, there is only one
                // EncoderParameter object in the array.
                myEncoderParameters = new EncoderParameters(1);

                // Save the bitmap as a JPEG file with quality level 25.
                myEncoderParameter = new EncoderParameter(myEncoder, qualidade);
                myEncoderParameters.Param[0] = myEncoderParameter;
                var novoNome = Path.ChangeExtension(fotoDestino, ".jpeg");
                myBitmap.Save(novoNome, myImageCodecInfo, myEncoderParameters);
                return novoNome;
            }

        }

        private static ImageCodecInfo GetEncoderInfo(String mimeType)
        {
            int j;
            ImageCodecInfo[] encoders;
            encoders = ImageCodecInfo.GetImageEncoders();
            for (j = 0; j < encoders.Length; ++j)
            {
                if (encoders[j].MimeType == mimeType)
                    return encoders[j];
            }
            return null;
        }
    }
}
