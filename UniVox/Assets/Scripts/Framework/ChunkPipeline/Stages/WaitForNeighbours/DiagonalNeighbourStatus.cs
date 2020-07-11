using System.Text;
using UniVox.Framework.Common;

namespace UniVox.Framework.ChunkPipeline.WaitForNeighbours
{
    public class DiagonalNeighbourStatus : INeighbourStatus
    {
        /// <summary>
        /// Bitmask where 1 indicates the neighbour is not valid
        /// </summary>
        private int bitmask = (1 << DiagonalDirectionExtensions.numDirections) - 1;

        public bool AllValid => bitmask == 0;

        public void AddNeighbour(int direction)
        {
            //Set the bit to 0
            bitmask &= ~(1 << direction);
        }

        public void RemoveNeighbour(int direction)
        {
            //Set the bit to 1
            bitmask |= 1 << direction;
        }

        public override string ToString()
        {
            StringBuilder builder = new StringBuilder();
            for (int i = 0; i < DiagonalDirectionExtensions.numDirections; i++)
            {
                var dir = (DiagonalDirection)i;
                builder.Append($"{dir}:{bitmask >> i & 1}");
            }
            return builder.ToString();
        }
    }

}