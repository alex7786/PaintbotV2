using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Paintbot
{
    class ColorDef
    {
        private String name, colorHex;
        private float xPos, yPos;
        private System.Drawing.Color color;

        public string Name { get => name; set => name = value; }
        public string ColorHex { get => colorHex; set => colorHex = value; }
        public Color Color { get => color; set => color = value; }
        public float XPos { get => xPos; set => xPos = value; }
        public float YPos { get => yPos; set => yPos = value; }

        public ColorDef(string name, string color, float xPos, float yPos)
        {
            this.name = name;
            this.colorHex = color;
            this.xPos = xPos;
            this.yPos = yPos;
            SetColor(this.colorHex);
        }

        public ColorDef(string parseColor)
        {
            var colorData = parseColor.Split('/');
            if(colorData.Length > 3)
            {
                this.name = colorData[0];
                this.colorHex = colorData[1];
                this.xPos = float.Parse(colorData[2]);
                this.yPos = float.Parse(colorData[3]);
                SetColor(this.colorHex);
            }
        }
       
        private void SetColor(string hexCode)
        {
            ColorConverter colorConverter = new ColorConverter();
            color = (Color)colorConverter.ConvertFromString("#" + hexCode);
        }
    }
}
