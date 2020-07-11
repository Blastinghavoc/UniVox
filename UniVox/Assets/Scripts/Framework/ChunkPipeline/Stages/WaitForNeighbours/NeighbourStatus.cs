using System.Text;
using UniVox.Framework.Common;

namespace UniVox.Framework.ChunkPipeline.WaitForNeighbours
{
    public class NeighbourStatus : INeighbourStatus
    {
        /// <summary>
        /// Bitmask where 1 indicates the neighbour is not valid
        /// </summary>
        private byte bitmask = (1 << DirectionExtensions.numDirections) - 1;

        public bool AllValid => bitmask == 0;

        public void AddNeighbour(int direction)
        {
            //Set the bit to 0
            bitmask &= (byte)~(1 << direction);
        }

        public void RemoveNeighbour(int direction)
        {
            //Set the bit to 1
            bitmask |= (byte)(1 << direction);
        }

        public override string ToString()
        {
            StringBuilder builder = new StringBuilder();
            for (int i = 0; i < DirectionExtensions.numDirections; i++)
            {
                var dir = (Direction)i;
                builder.Append($"{dir}:{bitmask >> i & 1}");
            }
            return builder.ToString();
        }
    }

}