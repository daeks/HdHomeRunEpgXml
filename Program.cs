//  Fairfield Tek L.L.C.
//  Copyright (c) 2016, Fairfield Tek L.L.C.
//  
//  
// THIS SOFTWARE IS PROVIDED BY FairfieldTek LLC ''AS IS'' AND ANY EXPRESS OR IMPLIED WARRANTIES,
// INCLUDING, BUT NOT LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR 
// PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL FAIRFIELDTEK LLC BE LIABLE FOR ANY DIRECT, INDIRECT, 
// INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF 
// SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND 
// ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR 
// OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH 
// DAMAGE. 
// 
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// 
//     http://www.apache.org/licenses/LICENSE-2.0
// 
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Xml;
using HdHomeRunEpgXml.Data;
using HdHomeRunEpgXml.Util;

namespace HdHomeRunEpgXml
{
    internal class Program
    {
        public static bool CheckCacheTimeStamp()
        {
            string timestring = DateTime.Now.ToString("yyyyMM") + ".txt";

            return Directory.Exists("cache") && File.Exists("cache/" + timestring);
        }

        public static void Decompress(FileInfo fileToDecompress)
        {
            using (var originalFileStream = fileToDecompress.OpenRead())
            {
                string currentFileName = fileToDecompress.FullName;
                string newFileName = currentFileName.Remove(currentFileName.Length - fileToDecompress.Extension.Length);

                using (var decompressedFileStream = File.Create(newFileName))
                {
                    using (var decompressionStream = new GZipStream(originalFileStream, CompressionMode.Decompress))
                    {
                        decompressionStream.CopyTo(decompressedFileStream);
                        Console.WriteLine("Decompressed: {0}", fileToDecompress.Name);
                    }
                }
            }
        }

        public static void DownloadImdb()
        {
            if (!Directory.Exists("cache"))
                Directory.CreateDirectory("cache");

            if (File.Exists("title.basics.tsv.gz"))
                File.Delete("title.basics.tsv.gz");

            if (File.Exists("title.basics.tsv"))
                File.Delete("title.basics.tsv");

            using (var client = new WebClient())
            {
                client.DownloadFile("https://datasets.imdbws.com/title.basics.tsv.gz", "title.basics.tsv.gz");
            }
            var info = new FileInfo("title.basics.tsv.gz");
            Decompress(info);
        }

        public static string[] FindTitle(string showTitle)
        {
            string L1 = getLetter(showTitle, 0);
            string L2 = getLetter(showTitle, 3);
            string L3 = getLetter(showTitle, 5);

            string filename = "cache/title.basics." + L1 + L2 + L3 + ".tsv";
            string data = File.ReadAllText(filename);
            var lines = data.Replace("\r", "").Split('\n');
            return (from line in lines where !string.IsNullOrEmpty(line) select line.Split('\t')).FirstOrDefault(elements =>
                elements[2].Equals(showTitle, StringComparison.InvariantCultureIgnoreCase) ||
                elements[3].Equals(showTitle, StringComparison.CurrentCultureIgnoreCase));
        }

        public static string getLetter(string word, int position)
        {
            word = word.ToLower();
            char letter = '_';
            if (word.Length > position)
                letter = word[position];

            if (!char.IsLetter(letter) && !char.IsNumber(letter))
                letter = '_';
            return letter.ToString();
        }

        private static void Main(string[] args)
        {
            if (args.Length > 0)
                if (args[0] == "?")
                {
                    Console.WriteLine("Argument expected: XML File Path.");
                    Console.WriteLine("Example: ");
                    Console.WriteLine("         HdHomeRunEpgXml " + '"' + "hdEpg.xml" + '"');
                    Console.WriteLine("         HdHomeRunEpgXml " + '"' + "C:\\hdEpg.xml" + '"');
                    Console.WriteLine("");
                    Console.WriteLine("You can also specify the device.");
                    Console.WriteLine("         HdHomeRunEpgXml " + '"' + "C:\\hdEpg.xml" + '"' + " <DeviceID>");
                }
            if (args.Length == 0)
            {
                Console.WriteLine("Argument expected: XML File Path.");
                Console.WriteLine("Example: ");
                Console.WriteLine("         HdHomeRunEpgXml " + '"' + "hdEpg.xml" + '"');
                Console.WriteLine("         HdHomeRunEpgXml " + '"' + "C:\\hdEpg.xml" + '"');
                Console.WriteLine("");
                Console.WriteLine("Press enter to exit.");
                Console.ReadLine();
                return;
            }

            if (!CheckCacheTimeStamp())
            {
                var watch = new Stopwatch();
                watch.Start();
                Console.WriteLine("Downloading IMDB Database.");
                DownloadImdb();
                Console.WriteLine("Parsing IMDB database, this can take a while...");
                ParseFile();
                watch.Stop();
                Console.WriteLine("Elapsed Time " + watch.Elapsed.Minutes);
                Console.ReadLine();
            }
            else
            {
                Console.WriteLine("Using cache.");
            }

            string selectedDevice = null;
            if (args.Length == 2)
                selectedDevice = args[1];

            IpAddressFinder.PrintLocalIPAddress();

            var tvShows = new List<XmlElement>();

            var doc = new XmlDocument();
            var xmlDeclaration = doc.CreateXmlDeclaration("1.0", "ISO-8859-1", null);
            var root = doc.DocumentElement;
            doc.InsertBefore(xmlDeclaration, root);
            var eleTv = doc.CreateElement(string.Empty, "tv", string.Empty);
            doc.AppendChild(eleTv);
            var processedChannel = new List<string>();

            //Fetch the devices registered.

            List<HdConnectDevice> devices = null;
            try
            {
                devices = JsonCalls.GetHdConnectDevices();
            }
            catch (Exception e)
            {
                Console.WriteLine("!!!!!It appears you do not have any HdHomeRun devices.!!!!!");
                Console.WriteLine("Press <enter> to exit");
                Console.ReadLine();
                Environment.Exit(0);
            }
            if (devices == null)
                Console.WriteLine("Devices are null!  Can't find recievers.");
            else
                try
                {
                    //For Each device.
                    foreach (var device in devices)
                    {
                        if (selectedDevice != null)
                            if (!selectedDevice.Trim().Equals(device.DeviceID.Trim(), StringComparison.InvariantCultureIgnoreCase))
                                continue;
                        Console.WriteLine("Processing Device: " + device.DeviceID);
                        //Get the Auth info

                        var discover = device.GetHdConnectDiscover();
                        //Get the channels

                        var channels = discover.GetHdConnectChannels();
                        //For each channel
                        foreach (var channel in channels)
                        {
                            //If we already processed this channel, then skip
                            if (processedChannel.Contains(channel.GuideNumber))
                                continue;
                            //Process the channel
                            processedChannel.Add(channel.GuideNumber);
                            //Add the tv shows
                            tvShows.AddRange(doc.ProcessChannel(eleTv, channel, discover.DeviceAuth));
                        }
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine("Error processing devices.");
                    Console.WriteLine(e);
                }

            //Append the shows to the list
            foreach (var element in tvShows)
                eleTv.AppendChild(element);

            try
            {
                //Save the file
                doc.Save(args[0]);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }

            Console.WriteLine("Finished.");
            Console.WriteLine("Epg file saved to: " + args[0]);
        }

        public static void ParseFile()
        {
            if (Directory.Exists("cache"))
                Directory.Delete("cache", true);

            Directory.CreateDirectory("cache");

            string line;
            long counter = 0;
            var file = new StreamReader(@"title.basics.tsv");
            while ((line = file.ReadLine()) != null)
            {
                if (string.IsNullOrEmpty(line))
                    continue;

                var elements = line.Split('\t');

                string L1 = getLetter(elements[2], 0);
                string L2 = getLetter(elements[2], 3);
                string L3 = getLetter(elements[2], 5);

                string filename = "cache/title.basics." + L1 + L2 + L3 + ".tsv";

                using (var writer = File.AppendText(filename))
                {
                    writer.WriteLine(line);
                    writer.Flush();
                }
                counter++;
                if (counter % 10000.00 == 0)
                    Console.WriteLine("Loaded " + counter + " rows so far.");
            }
        }
    }
}