namespace UniVox.Framework.Lighting
{
    public struct LightValue 
    {
        public const int IntensityRange = 16;

        //4 bits for dynamic light intensity, 4 bits for sun light intensity
        //ddddssss
        private byte bits;

        public int Sun { get => bits& 0xF; set => bits = (byte)((bits&0xF0)|value); }
        public int Dynamic { get => (bits>>4)& 0xF; set => bits = (byte)((bits&0xF)|value<<4); }
    }
} 