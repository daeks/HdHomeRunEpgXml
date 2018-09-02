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
using System.Linq;
using System.Xml;
using HdHomeRunEpgXml.Data;

namespace HdHomeRunEpgXml.Util
{
    public static class HdHomeRunXml
    {
        private const string DateFormat = "yyyyMMddHHmmss";

        private static XmlElement CreateElement(XmlDocument doc, string name, string value, string atrib = null, string attribVal = null)
        {
            //Add the title
            var eleTitle = doc.CreateElement(string.Empty, name, string.Empty);
            eleTitle.SetAttribute("lang", "en");
            if (atrib != null && attribVal != null)
                eleTitle.SetAttribute(atrib, attribVal);
            var eleTitleText = doc.CreateTextNode(value);
            eleTitle.AppendChild(eleTitleText);
            return eleTitle;
        }

        public static string DateTimeToEpisode(int startDt)
        {

            var dt = Time.UnixTimeStampToDateTime(startDt);
            string season = dt.ToString("yyyy");
            string episode = dt.ToString("MMddhhmm");
            return season + " . " + episode + " . 0/1";
        }

        public static string DatetimeToEpisodeFriendly(int startDt)
        {
            var dt = Time.UnixTimeStampToDateTime(startDt);
            string season = dt.ToString("yyyy");
            string episode = dt.ToString("MMddhhmm");
            return "S" + season + "E" + episode;
        }

        public static List<string> GetWords(string text)
        {
            var punctuation = text.Where(char.IsPunctuation).Distinct().ToArray();
            return text.Split().Select(x => x.Trim(punctuation)).ToList();
        }

        public static XmlElement LoadShow(this XmlDocument doc, string guidName, HdConnectProgram program)
        {
            Console.WriteLine("Processing Show: " + program.Title);

            //Create the entry for the program
            var eleProgram = doc.CreateElement(string.Empty, "programme", string.Empty);
            eleProgram.SetAttribute("start", Time.UnixTimeStampToDateTime(program.StartTime).ToLocalTime().ToString(DateFormat) + " " + Time.GetOffset());
            eleProgram.SetAttribute("stop", Time.UnixTimeStampToDateTime(program.EndTime).ToLocalTime().ToString(DateFormat) + " " + Time.GetOffset());
            eleProgram.SetAttribute("channel", guidName);

            //Add the title
            eleProgram.AppendChild(CreateElement(doc, "title", program.Title));

            //Add a sub-title
            eleProgram.AppendChild(CreateElement(doc, "sub-title", program.EpisodeTitle));

            //Add a Description 
            eleProgram.AppendChild(CreateElement(doc, "desc", program.Synopsis));

            eleProgram.AppendChild(CreateElement(doc, "credits", ""));

            //What image to show for the thumbnail
            eleProgram.AppendChild(CreateElement(doc, "credits", ""));

            eleProgram.AppendChild(CreateElement(doc, "icon", "", "src", program.ImageURL));

            //Just put normal subtitles, HdHomeRun doesn't provide this information
            eleProgram.AppendChild(CreateElement(doc, "subtitles", "", "type", "teletext"));

            //Just put stereo, HdHomeRun doesn't provide this info
            var eleAudio = doc.CreateElement(string.Empty, "audio", string.Empty);
            var eleAudioChild = doc.CreateElement(string.Empty, "stereo", string.Empty);
            var eleAudioChildText = doc.CreateTextNode("stereo");
            eleAudioChild.AppendChild(eleAudioChildText);
            eleAudio.AppendChild(eleAudioChild);
            eleProgram.AppendChild(eleAudio);


            bool invalidPreviousShown = false;
            List<string> filtersToAdd = new List<string>();
            var imdbData = Program.FindTitle(program.Title);

            if (imdbData != null)
            {
                filtersToAdd.Add(imdbData.TitleType);
                foreach (string word in imdbData.Genres)
                    if (!word.Equals("\\N", StringComparison.InvariantCultureIgnoreCase))
                        if (!filtersToAdd.Contains(word.ToLower().Trim()))
                            filtersToAdd.Add(word.ToLower().Trim());

                if (!filtersToAdd.Contains(imdbData.TitleType.ToLower()))
                    filtersToAdd.Add(imdbData.TitleType.ToLower());
            }

            if (program.Filter != null && program.Filter.Count > 0)
                foreach (string filter in program.Filter)
                {
                    string filterstringLower = filter.ToLower().Trim();
                    if (filtersToAdd.Contains(filterstringLower))
                        continue;

                    if (filterstringLower.Equals("news", StringComparison.InvariantCultureIgnoreCase))
                        invalidPreviousShown = true;

                    //# Does HdHomeRun think it is a movie?
                    if (filterstringLower.Equals("movies", StringComparison.InvariantCultureIgnoreCase) ||
                        filterstringLower.Equals("movie", StringComparison.InvariantCultureIgnoreCase))
                    {

                        Console.WriteLine("----------------------  MOVIE TAG IN HdHomeRun FILTER");
                        //# Does the movie not exist in the IMDB database?
                        //#if (imdbData == 0 ):
                        //# print ("HdHomeRun ------------------------> Is Movie!!!!!")
                        //# No, so lets just trust HdHomeRun
                        if (string.IsNullOrEmpty(program.EpisodeNumber))
                            if (!filtersToAdd.Contains("movie"))
                                filtersToAdd.Add("movie");
                        continue;
                    }
                    if (!filtersToAdd.Contains(filterstringLower))
                        filtersToAdd.Add(filterstringLower);
                }
            List<string> words = GetWords(program.Title.ToLower());
            if (words.Contains("news"))
            {
                eleProgram.AppendChild(CreateElement(doc, "category", "news"));
                invalidPreviousShown = true;
            }
            else
            {
                if (words.Contains("sports") ||
                    words.Contains("football") ||
                    words.Contains("soccer") ||
                    words.Contains("baseball") ||
                    words.Contains("dance") ||
                    words.Contains("dancing") ||
                    words.Contains("olympics") ||
                    words.Contains("cycling") ||
                    words.Contains("billiards") ||
                    words.Contains("basketball") ||
                    words.Contains("athletics") ||
                    words.Contains("boxing") ||
                    words.Contains("cricket") ||
                    words.Contains("fencing"))
                    filtersToAdd.Add("sports");
            }

            bool foundMovie = false;

            if (string.IsNullOrEmpty(program.EpisodeNumber))
            {
                if (filtersToAdd.Any(filter =>
                    filter.Equals("movie", StringComparison.InvariantCultureIgnoreCase) ||
                    filter.Equals("movies", StringComparison.InvariantCultureIgnoreCase)))
                {
                    Console.WriteLine("-------------------------->Found MOVIE");
                    foundMovie = true;
                    eleProgram.AppendChild(CreateElement(doc, "category", "movie"));
                }
            }

            foreach (string filter in filtersToAdd)
            {
                if (filter.Equals("series", StringComparison.InvariantCultureIgnoreCase) ||
                    filter.Equals("movie", StringComparison.InvariantCultureIgnoreCase))
                {
                    continue;
                }
                eleProgram.AppendChild(CreateElement(doc, "category", filter.ToLower().Trim()));
            }

            if (!string.IsNullOrEmpty(program.EpisodeNumber))
            {
                eleProgram.AppendChild(CreateElement(doc, "episode-num", program.EpisodeNumber, "system", "onscreen"));
                var parts = program.EpisodeNumber.Split('E');
                string season = parts[0].Substring(1);
                string episode = parts[1];
                string v = season + " . " + episode + " . 0/1";
                eleProgram.AppendChild(CreateElement(doc, "episode-num", v, "system", "xmltv_ns"));
                //Add a category of series.
                eleProgram.AppendChild(CreateElement(doc, "category", "series"));
            }
            else
            {
                if (!foundMovie)
                {
                    eleProgram.AppendChild(CreateElement(doc, "episode-num", DatetimeToEpisodeFriendly(program.StartTime), "system", "onscreen"));
                    eleProgram.AppendChild(CreateElement(doc, "episode-num", DateTimeToEpisode(program.StartTime), "system", "xmltv_ns"));
                    eleProgram.AppendChild(CreateElement(doc, "category", "series"));
                }
            }

            //Something is funny with previously shown, it appears to have info into the future
            //I think it has to do w/ some of the feeds being west coast and me
            //being on the east coast.
            //So just to fix this, if the previous shown > the air date we just don't include it.
            if (program.OriginalAirdate <= 0 || invalidPreviousShown)
                return eleProgram;
            if (Time.UnixTimeStampToDateTime(program.OriginalAirdate).ToLocalTime().AddDays(1) <
                Time.UnixTimeStampToDateTime(program.StartTime).ToLocalTime())
            {
                //Add when it was previously shown
                var prevShown = doc.CreateElement(string.Empty, "previously-shown", string.Empty);
                prevShown.SetAttribute("start",
                    Time.UnixTimeStampToDateTime(program.OriginalAirdate).ToLocalTime().AddDays(1).ToString(DateFormat) + " " + Time.GetOffset());
                eleProgram.AppendChild(prevShown);
            }
            else
            {
                Console.WriteLine("Previous Shown in FUTURE....  Watch out Marty McFly!!!!!!!!!!!!!!!!!!!!!!!!!!!!");
            }

            return eleProgram;
        }

        public static List<XmlElement> ProcessChannel(this XmlDocument doc, XmlElement eleTv, HdConnectChannel channel, string deviceAuth)
        {
            Console.WriteLine("Processing Channel: " + channel.GuideNumber + " : " + channel.GuideName);

            var tvShows = new List<XmlElement>();

            var eleChan = doc.CreateElement(string.Empty, "channel", string.Empty);
            eleChan.SetAttribute("id", channel.GuideName);
            eleTv.AppendChild(eleChan);

            var eleDn1 = doc.CreateElement(string.Empty, "display-name", string.Empty);
            var eleDn1T = doc.CreateTextNode(channel.GuideName);
            eleDn1.AppendChild(eleDn1T);
            eleChan.AppendChild(eleDn1);

            var eleDn2 = doc.CreateElement(string.Empty, "display-name", string.Empty);
            var eleDn2T = doc.CreateTextNode(channel.GuideNumber);
            eleDn2.AppendChild(eleDn2T);
            eleChan.AppendChild(eleDn2);

            if (!string.IsNullOrEmpty(channel.Affiliate))
            {
                var eleDn3 = doc.CreateElement(string.Empty, "display-name", string.Empty);
                var eleDn3T = doc.CreateTextNode(channel.Affiliate);
                eleDn3.AppendChild(eleDn3T);
                eleChan.AppendChild(eleDn3);
            }

            if (!string.IsNullOrEmpty(channel.ImageURL))
            {
                var eleImageUrl = doc.CreateElement(string.Empty, "icon", string.Empty);
                eleImageUrl.SetAttribute("url", channel.ImageURL);
                eleChan.AppendChild(eleImageUrl);
            }

            double maxTimeStamp = 0;
            foreach (var program in channel.Guide)
            {
                tvShows.Add(LoadShow(doc, channel.GuideName, program));
                if (program.EndTime > maxTimeStamp)
                    maxTimeStamp = program.EndTime;
            }

            //Move the timestamp forward one second to start next hour
            maxTimeStamp++;

            int counter = 0;

            try
            {
                //Each request represents 4 hours, so this will fetch 25 * 4 or 100 hours of programming
                while (counter < 42)
                {
                    //Request the next programming for the channel

                    var moreProgramming = JsonCalls.GetHdConnectChannelPrograms(deviceAuth, channel.GuideNumber, maxTimeStamp);
                    //Add the shows
                    foreach (var program in moreProgramming[0].Guide)
                    {
                        tvShows.Add(LoadShow(doc, channel.GuideName, program));
                        if (program.EndTime > maxTimeStamp)
                            maxTimeStamp = program.EndTime;
                    }
                    counter++;
                    //Move the timestamp forward one second to start next hour
                    maxTimeStamp++;
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("!!!!!!!!!!It appears you do not have the HdHomeRun Dvr service.!!!!!!!!!!");
            }

            return tvShows;
        }
    }
}