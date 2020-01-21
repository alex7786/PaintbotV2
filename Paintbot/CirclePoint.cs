using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Paintbot
{
    class CirclePoint
    {
        private int centerX, centerY, width, height, heightPlusWidth, diameter;
        private float radius;
        private string color;

        public int CenterX { get => centerX; set => centerX = value; }
        public int CenterY { get => centerY; set => centerY = value; }
        public int Width { get => width; set => width = value; }
        public int Height { get => height; set => height = value; }
        public int HeightPlusWidth { get => heightPlusWidth; set => heightPlusWidth = value; }
        public float Radius { get => radius; set => radius = value; }
        public string Color { get => color; set => color = value; }
        public int Diameter { get => diameter; set => diameter = value; }

        public CirclePoint(int centerX, int centerY, int width, int height, string color)
        {
            this.centerX = centerX;
            this.centerY = centerY;
            this.width = width;
            this.height = height;
            this.color = color;

            heightPlusWidth = width + height;
            if(width < height)
            {
                radius = width/2;
                height = width;
            }
            else
            {
                radius = height/2;
                width = height;
            }

            diameter = (int)(2 * radius);
        }

        public int GetCircleXmin(){ return CenterX - (int)Radius; }
        public int GetCircleYmin(){ return CenterY - (int)Radius; }

        public int GetCircleXmax() { return CenterX + (int)Radius; }
        public int GetCircleYmax() { return CenterY + (int)Radius; }
    }
}
