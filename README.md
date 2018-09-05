# HdHomeRunEpgXml #
# This program is provided with no warrenty and is for educational purposes only. #

This program downloads the programming for your connected HdHomeRun devices and converts it to a TVXML file which you can then use on Plex or any other Media Center.  To recieve more than 4 hours of programming you must subscribe to either SiliconDust's DVR service  or  the Premium Tv Package which provides the extended EPG data that this program uses.

**9/02/2018**
We have enhanced both the Python and C# application significantly.  We now have a very accuract EPG thanks to the efforts of all those who have contributed there work to the project.  I personally want to thank everyone for their time developing and testing the scripts.

-Vince

**8/31/2018**
I have successfully integrated the IMDb Dataset "title.basics.tsv.gz" into the engine to validate metadata.  This was to increase the accuracy of detecting movies and such.  With this change both the C# and Python first run of each month will take 30 - 45 minutes to run since it will download a new copy of the file from https://www.imdb.com/interfaces/.

-Vince



**For Windows**
  To run the program on windows, either download and compile the program (Requires Visual Studio 2017 or greater) or download the Release.Zip file in the project.
  
**For Linux/Others**
  The file HdHomeRun.py is a python 3.7 program which is a translation of the C# program.  This python program requires Pip to be installed as well as the urllib3 library.
 
**For Support, visit https://forums.plex.tv/t/for-those-who-purchase-the-hdhomerun-primium-live-video-service-this-is-how-you-can-get-your-epg/300886/69

**Note:**  It is recommended that you schedule the download of the EPG only ONCE per day, plex will read the TVXML file during maintenance in the middle of the night.  On Linux, use crontab -e to schedule, and for windows use the System Task Scheduler.

## Running with Docker

This repository includes a Dockerfile that will build an Alpine Linux Docker image that includes all the dependencies to run the `hdhomerun.py` script to generate the hdhomerun.xml file.

Steps to use:

* Make sure you have docker installed and available in your `$PATH`.
* Download the `Dockerfile` file in the ./Docker to an empty directory.
* Change into the new directory with the Dockerfile in it.
* Run `docker build -t hdhomerunepgxml .` (don't miss the "." or alternatively specify the path to the dockerfile instead of '.' I.E. /home/plex/Docker)
* Run `docker run --rm hdhomerunepgxml > /[directory]/hdhomerun.xml` where [directory] is the place you want the file stored.
You can schedule the docker run command above as a cronjob.

Note:  To update the docker image when there is an update to the code you need to do the following

* run 'docker build --no-cache -t hdhomerunepgxml .' (again, don't miss the ".")

This will re-pull the updates and update the docker image.   To brute force this you could put in a cron job to run this every few days until code updates stabalize, or run it manually when you see an update you want is posted.

## Running with Windows
* Download from the latest release from githib page. 
* You will see Release.MM.DD.YYYY.R.zip. Unzip that onto your system in whatever folder you want. 
* Open a command window and go that directory
* run HdHomeRunEpgXml full path to where you want the xmltv file to be so for example, "HdHomeRunEpgXml c:\craptastic\xmltv.xml"
* It will run and when it is done you will end up with file c:\craptastic\xmltv.xml
* You can automate it however you like. Windows scheduler is fine.

## Note:
* You need to be on the network with your hdhomerun device
* If you don’t subscribe to the hdhomerun DVR service nor the premium TV you will only get 4 hours worth of programming.
* If you subscribe to just the premium Tv you will get 1 Week of programming
* If you subscribe to the DVR service you will get 2 Weeks of programming

## Important
* HdHomeRun is a registered Trademark of SiliconDust 
* HdHomeRun/SiliconDust has NO AFFILIATION with this project 
* Plex is a registered Trademark of Plex
* Plex has NO AFFILIATION with this project

**Fairfield Tek L.L.C.**

**Copyright (c) 2016, Fairfield Tek L.L.C.**
  
  
 THIS SOFTWARE IS PROVIDED BY FAIRFIELDTEK LLC ''AS IS'' AND ANY EXPRESS OR IMPLIED WARRANTIES,
 INCLUDING, BUT NOT LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR 
 PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL FAIRFIELDTEK LLC BE LIABLE FOR ANY DIRECT, INDIRECT, 
 INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF 
 SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND 
 ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR 
 OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH 
 DAMAGE. 
 
 Licensed under the Apache License, Version 2.0 (the "License");
 you may not use this file except in compliance with the License.
 You may obtain a copy of the License at
 
     http://www.apache.org/licenses/LICENSE-2.0
 
 Unless required by applicable law or agreed to in writing, software
 distributed under the License is distributed on an "AS IS" BASIS,
 WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 See the License for the specific language governing permissions and
 limitations under the License.

