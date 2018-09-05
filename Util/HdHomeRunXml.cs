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
            string season = (int.Parse(dt.ToString("yyyy")) - 1).ToString();
            string episode = (int.Parse(dt.ToString("MMddhhmm")) - 1).ToString();
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
            //Console.WriteLine("Processing Show: " + program.Title);

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
            bool IsMovie = false;
            bool IsSeries = true;
            bool IsNews = false;
            bool IsSports = false;
            MovieData imdbData = null;
            string programRating = string.Empty;

            if (program.Filter != null && program.Filter.Count > 0)
                foreach (string word in program.Filter)
                {
                    if (word.ToLower().Equals("movies"))
                    {
                        IsMovie = true;
                        IsSeries = false;
                        imdbData = Program.FindMovieTitle(program.Title);
                    }
                }

            if (IsMovie == false)
            {
                if (program.EpisodeNumber != null)
                {
                    imdbData = Program.FindSeriesTitle(program.Title);
                    IsSeries = true;
                    IsMovie = true;
                }
                else
                {
                    imdbData = Program.FindMovieTitle(program.Title);
                    IsMovie = true;
                    if (imdbData != null)
                    {
                        if (imdbData.TitleType == "movie")
                        {
                            IsMovie = true;
                            IsSeries = false;
                        }
                        else
                        {
                            IsSeries = true;
                            IsMovie = false;
                        }
                    }
                }
            }

            List<string> NewsWords = new List<string>(){"news","cnn","msnbc","weather","newsline"};
            List<string> SportsWords = new List<string>()
            {
                "sports","football","soccer","baseball","dance","dancing",
                "olympics","cycling","billiards","basketball","athletics","boxing",
                "cricket","fencing","pga","wrestling","wwe","tennis","sportscenter"
            };

            foreach (string word in GetWords(program.Title))
            {
                if (NewsWords.Contains(word))
                {
                    IsNews = true;
                    IsMovie = false;
                    IsSeries = true;
                    IsSports = false;
                    invalidPreviousShown = true;
                    break;
                }
                else if (SportsWords.Contains(word))
                {
                    IsNews = false;
                    IsMovie = false;
                    IsSeries = true;
                    IsSports = true;
                    break;
                }
            }
            List<string> filtersToAdd = new List<string>();
            if (imdbData != null)
            {
                programRating = Program.FindRating(imdbData.TitleID);

                //Console.WriteLine("Chk: " + program.Title + " ----> " + imdbData.TitleType);

                if (imdbData.TitleType.Equals("movie", StringComparison.InvariantCultureIgnoreCase) && !IsSports && !IsNews)
                {
                    IsMovie = true;
                    IsSeries = false;
                }
                if (imdbData.TitleType.Equals("series", StringComparison.InvariantCultureIgnoreCase))
                {
                    IsMovie = false;
                    IsSeries = true;
                }

                foreach (string word in imdbData.Genres)
                {
                    if (!word.Equals("\\n") && !word.Equals("movie") && !word.Equals("news") && !word.Equals("series") && !word.Equals("sports") && !filtersToAdd.Contains(word))
                    {
                        filtersToAdd.Add(word);
                    }
                }

                {
                    string word = imdbData.OriginalTitleType;
                    if (!word.Equals("\\n") && !word.Equals("movie") && !word.Equals("news") && !word.Equals("series") && !word.Equals("sports") && !filtersToAdd.Contains(word))
                    {
                        filtersToAdd.Add(word);
                    }

                }
            }
            //else
            //{
            //    //Console.WriteLine("Chk: " +program.Title + "---> Unknown");
            //}

            if (program.Filter != null && program.Filter.Count > 0)
            {
                foreach (string filter in program.Filter)
                {
                    string word = filter.ToLower();
                    if (filtersToAdd.Contains(word))
                        continue;

                    if (word.Equals("news",StringComparison.InvariantCultureIgnoreCase))
                    {
                        IsNews = true;
                        IsSeries = true;
                        IsMovie = false;
                        IsSports = false;
                        invalidPreviousShown = true;
                        continue;
                    }
                    if (word.Equals("sports", StringComparison.InvariantCultureIgnoreCase))
                    {
                        IsNews = false;
                        IsSeries = true;
                        IsMovie = false;
                        IsSports = true;
                        continue;
                    }
                    if (word.Equals("movies", StringComparison.InvariantCultureIgnoreCase) || word.Equals("movies", StringComparison.InvariantCultureIgnoreCase))
                    {
                        if (!IsSports && !IsNews)
                            if (imdbData == null)
                            {
                                if (program.EpisodeNumber != null)
                                {
                                    IsSeries = true;
                                    IsMovie = false;
                                }
                                else
                                {
                                    IsMovie = true;
                                    IsSeries = false;
                                }
                            }
                    }
                    else
                    {
                        if (!word.Equals("\\n") && !word.Equals("movie") && !word.Equals("news") && !word.Equals("series") && !word.Equals("sports") && !filtersToAdd.Contains(word))
                            filtersToAdd.Add(word);
                    }
                }
            }

            if (!string.IsNullOrEmpty(program.EpisodeNumber) || IsNews || IsSeries)
            {
                IsMovie = false;
                IsSeries = true;
            }

            if (IsMovie)
            {
                eleProgram.AppendChild(CreateElement(doc, "category", "movie","lang","en"));
                eleProgram.AppendChild(CreateElement(doc, "category", "M0VIECHECK", "lang", "en"));
                }

            if (IsNews)
            {
                eleProgram.AppendChild(CreateElement(doc, "category", "news", "lang", "en"));
                eleProgram.AppendChild(CreateElement(doc, "category", "N3WSCHECK", "lang", "en"));
            }

            if (IsSports)
            {
                eleProgram.AppendChild(CreateElement(doc, "category", "sports", "lang", "en"));
                eleProgram.AppendChild(CreateElement(doc, "category", "SP0RTSCHECK", "lang", "en"));
                }

            if (IsSeries)
            {
                eleProgram.AppendChild(CreateElement(doc, "category", "series", "lang", "en"));
                eleProgram.AppendChild(CreateElement(doc, "category", "S3RIESCHECK", "lang", "en"));
            }

            if (!string.IsNullOrEmpty(program.EpisodeNumber))
            {
                eleProgram.AppendChild(CreateElement(doc, "episode-num", program.EpisodeNumber, "system", "onscreen"));
                var parts = program.EpisodeNumber.Split('E');
                string season = (int.Parse(parts[0].Substring(1).Trim()) - 1).ToString();
                string episode = (int.Parse(parts[1].Trim()) - 1).ToString();
                string v = season + "." + episode + ". 0/1";
                eleProgram.AppendChild(CreateElement(doc, "episode-num", v, "system", "xmltv_ns"));

                if (program.OriginalAirdate > 0 && !invalidPreviousShown)
                {
                    eleProgram.AppendChild(CreateElement(doc, "episode-num", Time.UnixTimeStampToDateTime(program.OriginalAirdate).ToLocalTime().AddDays(1).ToString(DateFormat), "system", "original-air-date"));
                }
                else
                {
                    eleProgram.AppendChild(CreateElement(doc, "episode-num", "", "system", "original-air-date"));
                }
            }
            else if (IsSeries)
            {
                eleProgram.AppendChild(CreateElement(doc, "episode-num", DatetimeToEpisodeFriendly(program.StartTime), "system", "onscreen"));
                eleProgram.AppendChild(CreateElement(doc, "episode-num", DateTimeToEpisode(program.StartTime), "system", "xmltv_ns"));
                eleProgram.AppendChild(CreateElement(doc, "episode-num", "", "system", "original-air-date"));
            }

            if (!string.IsNullOrEmpty(programRating))
            {
                var eleRating = doc.CreateElement(string.Empty, "rating", string.Empty);
                eleRating.SetAttribute("system", "IMDB");
                var eleRatingChild = doc.CreateElement(string.Empty, "value", string.Empty);
                var eleRatingChildText = doc.CreateTextNode(programRating);
                eleRatingChild.AppendChild(eleRatingChildText);
                eleRating.AppendChild(eleRatingChild);
                eleProgram.AppendChild(eleRating);
            }


            if (IsNews)
                Console.WriteLine("Chk: NEWS ---> " + program.Title);
            if (IsSports)
                Console.WriteLine("Chk: SPORTS--> " + program.Title);
            if (IsMovie)
                Console.WriteLine("Chk: MOVIE--->" + program.Title );
            if (!IsNews && !IsSports && IsSeries)
                Console.WriteLine("Chk: SERIES-->" + program.Title );


            //Something is funny with previously shown, it appears to have info into the future
            //I think it has to do w/ some of the feeds being west coast and me
            //being on the east coast.
            //So just to fix this, if the previous shown > the air date we just don't include it.
            if (program.OriginalAirdate <= 0 || invalidPreviousShown)
                return eleProgram;

            if (Time.UnixTimeStampToDateTime(program.OriginalAirdate).ToLocalTime().AddDays(1) >= Time.UnixTimeStampToDateTime(program.StartTime).ToLocalTime())
                return eleProgram;

            //Add when it was previously shown
            var prevShown = doc.CreateElement(string.Empty, "previously-shown", string.Empty);
            prevShown.SetAttribute("start",
                Time.UnixTimeStampToDateTime(program.OriginalAirdate).ToLocalTime().AddDays(1).ToString(DateFormat) + " " + Time.GetOffset());
            eleProgram.AppendChild(prevShown);
            //else
            //{
            //   // Console.WriteLine("Previous Shown in FUTURE....  Watch out Marty McFly!!!!!!!!!!!!!!!!!!!!!!!!!!!!");
            //}

          
            

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
                eleImageUrl.SetAttribute("src", channel.ImageURL);
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
                //Console.WriteLine("!!!!!!!!!!It appears you do not have the HdHomeRun Dvr service.!!!!!!!!!!");
            }

            return tvShows;
        }
    }
}
