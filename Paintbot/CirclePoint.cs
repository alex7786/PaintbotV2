using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Paintbot
{
    class CirclePoint
    {
        private int centerX, centerY, width, height, heightPlusWidth;

        public int CenterX { get => centerX; set => centerX = value; }
        public int CenterY { get => centerY; set => centerY = value; }
        public int Width { get => width; set => width = value; }
        public int Height { get => height; set => height = value; }
        public int HeightPlusWidth { get => heightPlusWidth; set => heightPlusWidth = value; }

        public CirclePoint(int centerX, int centerY, int width, int height)
        {
            this.centerX = centerX;
            this.centerY = centerY;
            this.width = width;
            this.height = height;
            heightPlusWidth = width + height;
        }
    }
}
