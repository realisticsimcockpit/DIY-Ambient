using System;

namespace DIYAmbient.Core
{
    public static class AdalightProtocol
    {
        public const int BaudRate = 115200;
        public const int PacketLength = 186;
        public static byte[] Encode(Rgb[] colors)
        {
            if (colors == null || colors.Length != 60)
                throw new ArgumentException("Le firmware fourni attend toujours exactement 60 LED.");
            byte[] packet = new byte[PacketLength];
            packet[0] = (byte)'A'; packet[1] = (byte)'d'; packet[2] = (byte)'a';
            int n = colors.Length - 1;
            packet[3] = (byte)(n >> 8); packet[4] = (byte)n;
            packet[5] = (byte)(packet[3] ^ packet[4] ^ 0x55);
            for (int i = 0; i < 60; i++)
            {
                packet[6 + 3 * i] = colors[i].R;
                packet[7 + 3 * i] = colors[i].G;
                packet[8 + 3 * i] = colors[i].B;
            }
            return packet;
        }
    }
}
