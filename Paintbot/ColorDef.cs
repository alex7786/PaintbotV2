using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Paintbot
{
    class ColorDef
    {
        private String name, color;
        private float xPos, yPos;

        public ColorDef(string name, string color, float xPos, float yPos)
        {
            this.name = name;
            this.color = color;
            this.xPos = xPos;
            this.yPos = yPos;
        }

        public ColorDef(string parseColor)
        {
            var colorData = parseColor.Split('/');
            this.name = colorData[0];
            this.color = colorData[1];
            this.xPos = float.Parse(colorData[2]);
            this.yPos = float.Parse(colorData[3]);
        }

    }
}
