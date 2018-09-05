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

        public static Dictionary<int, MovieData> MovieIndex = new Dictionary<int, MovieData>();
        public static Dictionary<int, MovieData> SeriesIndex = new Dictionary<int, MovieData>();
        public static Dictionary<string, string> TitleRatings = new Dictionary<string, string>();

        public static void DownloadImdb()
        {
            if (File.Exists("title.basics.tsv.gz"))
                File.Delete("title.basics.tsv.gz");

            if (File.Exists("title.basics.tsv"))
                File.Delete("title.basics.tsv");

            using (var client = new WebClient())
            {
                client.DownloadFile("https://datasets.imdbws.com/title.basics.tsv.gz", "title.basics.tsv.gz");
            }

            var info = new FileInfo("title.basics.tsv.gz");
                            int mc = 0;
                int sc = 0;
            Decompress(info);
            string line;
            int counter = 0;
            var file = new StreamReader("title.basics.tsv");
            while ((line = file.ReadLine()) != null)
            {
                counter = counter + 1;

                if (string.IsNullOrEmpty(line))
                    continue;

                var elements = line.Split('\t');

                string TitleType = "series";

                if (elements[1].Equals("short", StringComparison.InvariantCultureIgnoreCase))
                    TitleType = "movie";
                else if (elements[1].Equals("movie", StringComparison.InvariantCultureIgnoreCase))
                    TitleType = "movie";
                else if (elements[1].Equals("tvMovice", StringComparison.InvariantCultureIgnoreCase))
                    TitleType = "movie";
                else if (elements[1].Equals("tvShort", StringComparison.InvariantCultureIgnoreCase))
                    TitleType = "movie";
                else if (elements[1].Equals("tvSpecial", StringComparison.InvariantCultureIgnoreCase))
                    TitleType = "movie";
                else if (elements[1].Equals("video", StringComparison.InvariantCultureIgnoreCase))
                    continue;
                else if (elements[1].Equals("tvSeries", StringComparison.InvariantCultureIgnoreCase))
                    TitleType = "series";
                else if (elements[1].Equals("tvEpisode", StringComparison.InvariantCultureIgnoreCase))
                    TitleType = "series";
                else if (elements[1].Equals("tvMiniSeries", StringComparison.InvariantCultureIgnoreCase))
                    TitleType = "series";
                else
                    continue;

                string showTitle = elements[2].Where(c => !InvalidChar.Contains(c)).Aggregate(string.Empty, (current, c) => current + c);

                int key = showTitle.GetHashCode();



                if (TitleType.Equals("movie", StringComparison.InvariantCultureIgnoreCase) && !MovieIndex.ContainsKey(key))
                {
                    MovieIndex.Add(key,
                        new MovieData()
                        {
                            Title = elements[2],
                            TitleID = elements[0],
                            TitleType = TitleType.ToLower(),
                            OriginalTitleType = elements[1].ToLower(),
                            Genres = elements[8].ToLower().Split(',').ToList()
                        });
                }
                else if (!SeriesIndex.ContainsKey(key))
                {
                    SeriesIndex.Add(key,
                        new MovieData()
                        {
                            Title = elements[2],
                            TitleID = elements[0],
                            TitleType = TitleType.ToLower(),
                            OriginalTitleType = elements[1].ToLower(),
                            Genres = elements[8].ToLower().Split(',').ToList()
                        });
                }
                else if (TitleType.Equals("movie", StringComparison.InvariantCultureIgnoreCase))
                {
                    mc++;
                }
                else
                {
                    sc++;
                }
            }

            Console.WriteLine("Total Movie Collisions = " + mc);
            Console.WriteLine("Total Series Collisions = " + sc);
        }

        public static void DownloadImdbRatings()
        {
            if (File.Exists("title.ratings.tsv.gz"))
                File.Delete("title.ratings.tsv.gz");

            if (File.Exists("title.ratings.tsv"))
                File.Delete("title.ratings.tsv");

            using (var client = new WebClient())
            {
                client.DownloadFile("https://datasets.imdbws.com/title.ratings.tsv.gz", "title.ratings.tsv.gz");
            }

            var info = new FileInfo("title.ratings.tsv.gz");

            Decompress(info);
            string line;
            int counter = 0;
            var file = new StreamReader("title.ratings.tsv");
            while ((line = file.ReadLine()) != null)
            {
                counter = counter + 1;
                if (string.IsNullOrEmpty(line))
                    continue;
                var elements = line.Split('\t');

                if (elements.Length == 3)
                {
                    TitleRatings.Add(elements[0], elements[1]);
                }
            }

            if (File.Exists("title.ratings.tsv.gz"))
                File.Delete("title.ratings.tsv.gz");
        }

        public static List<char> InvalidChar = new List<char>() { '!', '@', '#', '$', '%', '^', '&', '&', '*', '(', '(', ')', '_', '-', '+', '=', '{', '}', '[', ']', '|', '\\', ':', ';', '<', ',', '>', '>', '?', '/' };

        public static MovieData FindMovieTitle(string rshowTitle)
        {
            string showTitle = rshowTitle.Where(c => !InvalidChar.Contains(c)).Aggregate(string.Empty, (current, c) => current + c);

            int key = showTitle.GetHashCode();

            return MovieIndex.ContainsKey(key) ? MovieIndex[key] : null;
        }

        public static MovieData FindSeriesTitle(string rshowTitle)
        {
            string showTitle = rshowTitle.Where(c => !InvalidChar.Contains(c)).Aggregate(string.Empty, (current, c) => current + c);

            int key = showTitle.GetHashCode();

            return SeriesIndex.ContainsKey(key) ? SeriesIndex[key] : null;
        }

        public static string FindRating(string id)
        {
            return TitleRatings.ContainsKey(id) ? TitleRatings[id] : string.Empty;
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

            DownloadImdb();
            DownloadImdbRatings();

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


    }
}
