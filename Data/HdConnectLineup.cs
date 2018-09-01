using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HdHomeRunEpgXml.Data
{
   public class HdConnectLineup
    {
        public string GuideNumber {get; set;}
        public string GuideName {get; set;}
        public string VideoCodec {get; set;}
        public string AudioCodec {get; set;}
        public int HD {get; set;}
        public string URL {get; set;}
    }
}
