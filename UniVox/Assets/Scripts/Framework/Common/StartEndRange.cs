namespace UniVox.Framework.Common
{
    public struct StartEndRange
    {
        public int start;
        public int end;//Exclusive

        public int Length { get => end - start; }

        public StartEndRange(int start, int end)
        {
            this.start = start;
            this.end = end;
        }
    }
}