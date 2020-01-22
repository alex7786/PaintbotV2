using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Paintbot
{
    class CirclePoint
    {
        private float centerX, centerY, width, height, heightPlusWidth, diameter, radius;
        private string color;

        public float CenterX { get => centerX; set => centerX = value; }
        public float CenterY { get => centerY; set => centerY = value; }
        public float Width { get => width; set => width = value; }
        public float Height { get => height; set => height = value; }
        public float HeightPlusWidth { get => heightPlusWidth; set => heightPlusWidth = value; }
        public float Radius { get => radius; set => radius = value; }
        public string Color { get => color; set => color = value; }
        public float Diameter { get => diameter; set => diameter = value; }

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

            diameter = width; //same as heigth
        }

        public float GetCircleXmin(){ return CenterX - Radius; }
        public float GetCircleYmin(){ return CenterY - Radius; }

        public float GetCircleXmax() { return CenterX + Radius; }
        public float GetCircleYmax() { return CenterY + Radius; }
    }
}
