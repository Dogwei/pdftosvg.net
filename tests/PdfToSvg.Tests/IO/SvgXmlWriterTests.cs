﻿using NUnit.Framework;
using PdfToSvg.IO;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace PdfToSvg.Tests.IO
{
    public class SvgXmlWriterTests
    {
        [Test]
        public void Write()
        {
            var xml = new XElement("svg",
                new XElement("g", new XAttribute("fill", "black"), new XElement("path")),
                new XElement("text", "     ", new XElement("tspan", "     "))
                );

            using (var stringWriter = new StringWriter())
            {
                using (var xmlWriter = new SvgXmlWriter(stringWriter))
                {
                    xml.WriteTo(xmlWriter);
                }

                var actual = stringWriter.ToString();
                Assert.AreEqual(
                    "<svg>\n" +
                    "<g fill=\"black\">\n" +
                    "<path />\n" +
                    "</g>\n" +
                    "<text>     <tspan>     </tspan></text>\n" +
                    "</svg>", actual);
            }
        }
    }
}
