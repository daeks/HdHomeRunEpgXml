# Written by Vincent Gee
# Derived from the work at https://forum.libreelec.tv/thread/12228-tvheadend-epg-guide-from-hdhomerun/
# 8/26/2018
#
# Description:  Downloads the EPG from HdHomeRun's server and converts it to a XMLTV format
#				So it can be loaded into Plex.

#//  Fairfield Tek L.L.C.
#//  Copyright (c) 2016, Fairfield Tek L.L.C.
#//  
#//  
#// THIS SOFTWARE IS PROVIDED BY FAIRFIELDTEK LLC ''AS IS'' AND ANY EXPRESS OR IMPLIED WARRANTIES,
#// INCLUDING, BUT NOT LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR 
#// PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL FAIRFIELDTEK LLC BE LIABLE FOR ANY DIRECT, INDIRECT, 
#// INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF 
#// SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND 
#// ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR 
#// OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH 
#// DAMAGE. 
#// 
#// Licensed under the Apache License, Version 2.0 (the "License");
#// you may not use this file except in compliance with the License.
#// You may obtain a copy of the License at
#// 
#//     http://www.apache.org/licenses/LICENSE-2.0
#// 
#// Unless required by applicable law or agreed to in writing, software
#// distributed under the License is distributed on an "AS IS" BASIS,
#// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
#// See the License for the specific language governing permissions and
#// limitations under the License.

import sys, json, urllib3, datetime, subprocess,time,os
import xml.etree.cElementTree as ET
import gzip
import shutil
import csv
from datetime import datetime
from xml.dom import minidom
from pprint import pprint


TitleIndex = {}
invalidChars =['!','@','#','$','%','^','&','&','*','(','(',')','_','-','+','=','{','}','[',']','|','\\',':',';','<',',','>','>','?','/',' ','.','`',"'"]




def get_utc_offset_str():
    """
    Returns a UTC offset string of the current time suitable for use in the
    most widely used timestamps (i.e. ISO 8601, RFC 3339). For example:
    10 hours ahead, 5 hours behind, and time is UTC: +10:00, -05:00, +00:00
    """

    # Calculate the UTC time difference in seconds.

    timestamp = time.time()
    time_now = datetime.fromtimestamp(timestamp)
    time_utc = datetime.utcfromtimestamp(timestamp)
    utc_offset_secs = (time_now - time_utc).total_seconds()

    # Flag variable to hold if the current time is behind UTC.
    is_behind_utc = False

    # If the current time is behind UTC convert the offset
    # seconds to a positive value and set the flag variable.
    if utc_offset_secs < 0:
        is_behind_utc = True
        utc_offset_secs *= -1

    # Build a UTC offset string suitable for use in a timestamp.

    if is_behind_utc:
        pos_neg_prefix = "-"
    else:
        pos_neg_prefix = "+"

    utc_offset = time.gmtime(utc_offset_secs)
    utc_offset_fmt = time.strftime("%H:%M", utc_offset)
    utc_offset_str = pos_neg_prefix + utc_offset_fmt

    return utc_offset_str


def HdHomeRunTimeStampToDate(hdTimeStamp):
	return datetime.fromtimestamp(hdTimeStamp)# - (86400 * 365))

def ProcessProgram(xml, program, guideName):
	timezone_offset = get_utc_offset_str().replace(":","")

	#Create the "programme" element and set the Channel attribute to "GuideName" from json
	xmlProgram = ET.SubElement(xml, "programme", channel = guideName)

	#set the start date and time from the feed
	xmlProgram.set("start", HdHomeRunTimeStampToDate(program['StartTime']).strftime('%Y%m%d%H%M%S') + " " + timezone_offset)

	#set the end date and time from the feed
	xmlProgram.set("stop", HdHomeRunTimeStampToDate(program['EndTime']).strftime('%Y%m%d%H%M%S') + " " + timezone_offset)

	#Title
	ET.SubElement(xmlProgram, "title", lang="en").text = program['Title']
		
	#Sub Title
	if 'EpisodeTitle' in program:
		ET.SubElement(xmlProgram, "sub-title", lang="en" ).text = program['EpisodeTitle']

	#Description
	if 'Synopsis' in program:
		ET.SubElement(xmlProgram, "desc").text = program['Synopsis']

	#Credits
	#We add a blank entry to satisfy Plex
	ET.SubElement(xmlProgram,"credits").text = ""
	
	invalidPreviousShown = False
				
	if 'ImageURL' in program:
		ET.SubElement(xmlProgram, "icon", src=program['ImageURL'])

	FiltersToAdd = []		

	xmlAudio = ET.SubElement(xmlProgram,"audio")
	ET.SubElement( xmlAudio, "stereo").text = "stereo"
	ET.SubElement(xmlProgram, "subtitles", type="teletext")	

	IsMovie = False
	IsSeries = True
	IsNews = False
	IsSports = False


	if ('Filter' in program):
		for word in program['Filter']:
			if word.lower() == 'movies':
				IsMovie  = True
				IsSeries = False
				imdbData =  FindMovieTitle(program['Title'])
				break
	
	if IsMovie == False:
		#Well... if it has an epidsode title it must be a series!
		if ('EpisodeNumber' in program):
			imdbData = FindSeriesTitle(program['Title'])
			IsSeries = True
			IsMovie = False
		else:
			imdbData =  FindMovieTitle(program['Title'])
			IsMovie = True
			if (imdbData != 0):
				if (imdbData[0] == "movie"):
					IsMovie = True
					IsSeries = False
			else:
					IsSeries = True
					IsMovie = False

	
	#We will split the words of the title up, and do some
	#basic checks on it for news and sports.
	words = str(program['Title']).lower().split()

	if ('news' in words) or ('cnn' in words) or ('msnbc' in words) or ('weather' in words) or ('newsline' in words):
		IsNews = True
		IsMovie = False
		IsSeries = True
		invalidPreviousShown = True
	else:
		if	('sports' in words) or ('football' in words) or ('soccer' in words) or ('baseball' in words) or ('dance' in words) or ('dancing' in words):
			IsSports=True
			IsMovie = False
			IsSeries = True
		else:
			if ('olympics' in words) or ('cycling' in words) or ('billiards' in words) or ('basketball' in words) or ('athletics' in words) or ('boxing' in words):
				IsSports=True
				IsMovie = False
				IsSeries = True
			else:
				if ('cricket' in words) or ('fencing' in words) or ('pga' in words) or ('wrestling' in words) or ('wwe' in words) or ('tennis' in words) or ('sportscenter' in words):
					IsSports=True
					IsMovie = False
					IsSeries = True															

	if (imdbData != 0):
		
		WriteLog ("Chk: " + program['Title'] + "--->" + str( imdbData[0] ) )

		if str( imdbData[0] ) == "movie" and not IsSports and not IsNews:
			IsMovie = True
			IsSeries = False

		if str( imdbData[0] ) == "series":
			IsMovie = False
			IsSeries = True

		words = str( imdbData[1] ).lower().split( ',' )
		
		for xword in words:
			word = str(xword).lower().strip()
			if (word != "\\n" and word != "movie" and word != "news" and word !="series" and word !="sports"):
				if (word not in FiltersToAdd ):
					FiltersToAdd.append(word)

		word = str(imdbData[2]).strip().lower()
		if (word != "\\n" and word != "movie" and word != "news" and word !="series" and word !="sports"):
			if (word not in FiltersToAdd ):
				FiltersToAdd.append(word)

	else:

		WriteLog ("Chk: " + program['Title'] + "---> Unknown")

	#Now we process the Filters given to us by HdHomeRun
	if 'Filter' in program:
		for filter in program['Filter']:
			word = str(filter).lower().strip()
			if (word in FiltersToAdd):
				continue

			if (word == "news"):
				IsNews = True
				IsSeries= True
				IsMovie = False
				invalidPreviousShown=True
				continue

			if (word == "sports"):
				IsSports = True
				IsMovie = False
				IsSeries = True
				continue

			#Does HdHomeRun think it is a movie?
			if ( word == "movies" or word == "movie" ):
				if (not ( IsSports or IsNews )):
					#Does the movie not exist in the IMDB database?
					if ( imdbData == 0 ):
						if ('EpisodeNumber' in program):
							IsSeries = True
							IsMovie = False
						else:
							IsMovie = True
			else:
				if ( word != "\\n" and word != "movie" and word != "movies" and word != "news" and word !="series" and word !="sports"):
					if (word not in FiltersToAdd ):
						FiltersToAdd.append( word )

	if (IsNews):
		invalidPreviousShown = True


	if ('EpisodeNumber' in program   or IsNews or IsSeries):
		IsMovie = False
		IsSeries = True
	
	if (IsMovie == True):
		ET.SubElement(xmlProgram, "category",lang="en").text = "movie"
		ET.SubElement(xmlProgram, "category",lang="en").text = "M0VIECHECK"
	
	if (IsNews):
		ET.SubElement(xmlProgram, "category",lang="en").text = "news"
		ET.SubElement(xmlProgram, "category",lang="en").text = "N3WSCHECK"
		IsSeries = True
	
	if (IsSports):
		ET.SubElement(xmlProgram, "category",lang="en").text = "sports"
		ET.SubElement(xmlProgram, "category",lang="en").text = "SP0RTSCHECK"
		IsSeries = True

	if (IsSeries == True):
		ET.SubElement(xmlProgram, "category",lang="en").text = "series"
		ET.SubElement(xmlProgram, "category",lang="en").text = "S3RIESCHECK"


	for xfilter in FiltersToAdd:
		word = str(xfilter).strip().lower()
		if (word != "\\n" and word != "movie" and word != "news" and word !="series" and word !="sports"):
			ET.SubElement(xmlProgram, "category",lang="en").text = word



	if ( ('EpisodeNumber') in program ) :

		#add the friendly display
		ET.SubElement(xmlProgram, "episode-num", system="onscreen").text = program['EpisodeNumber']
		
		#Fake the xml version
		en = program['EpisodeNumber']
		
		parts = en.split("E")
		season =int( parts[0].replace("S","")) - 1
		episode = int(parts[1]) -1
		
		#Assign the fake xml version
		ET.SubElement(xmlProgram, "episode-num", system="xmltv_ns").text = ( str ( season ) + "." + str ( episode )  + ".0/1")

		if 'OriginalAirdate' in program:

			if program['OriginalAirdate'] > 0 and not invalidPreviousShown and (program['OriginalAirdate'] + 86400) < program['StartTime']:
				ET.SubElement(xmlProgram, "episode-num", system="original-air-date").text = (HdHomeRunTimeStampToDate(program['OriginalAirdate'] + 86400 ).strftime('%Y%m%d%H%M%S') + " " + timezone_offset)
			else:
				ET.SubElement(xmlProgram, "episode-num", system="original-air-date")
		else:
			ET.SubElement(xmlProgram, "episode-num", system="original-air-date")				
			
	else:
		
		if (IsSeries):
			if (IsSports):
				counter = GetSportsCounter()
				ET.SubElement(xmlProgram, "episode-num", system="xmltv_ns").text = DateTimeToEpisode(program['StartTime'] , counter)
				ET.SubElement(xmlProgram, "episode-num", system="onscreen").text = DateTimeToEpisodeFriendly(program['StartTime'] , counter)
			else:
				ET.SubElement(xmlProgram, "episode-num", system="xmltv_ns").text = DateTimeToEpisode(program['StartTime'] , 0)
				ET.SubElement(xmlProgram, "episode-num", system="onscreen").text = DateTimeToEpisodeFriendly(program['StartTime'] , 0)
			ET.SubElement(xmlProgram, "episode-num", system="original-air-date")

	
	if 'OriginalAirdate' in program:
		#there is something funny w/ prev shown, this tries to address it.
		#sometimes the prevshown is in the future, which kinda screws up things
		#so if it's in the future I no longer add it.

		if program['OriginalAirdate'] > 0 and not invalidPreviousShown and (program['OriginalAirdate'] + 86400) < program['StartTime']:
			#The 86400 is because the HdHomeRun feed is off by a day, this fixes that.
			ET.SubElement(xmlProgram, "previously-shown", start = HdHomeRunTimeStampToDate(program['OriginalAirdate'] + 86400 ).strftime('%Y%m%d%H%M%S') + " " + timezone_offset)
			
			

	#Return the endtime so we know where to start from on next loop.
	return program['EndTime']

	
def processChannel(xml, data, deviceAuth):


	
	WriteLog ("Processing Channel: " + data.get('GuideNumber') + " " + data.get('GuideName'))

	#channel
	xmlChannel = ET.SubElement(xml, "channel", id = data.get('GuideName'))
		
	#display name
	ET.SubElement(xmlChannel, "display-name").text = data.get('GuideName')
		
	#display name
	ET.SubElement(xmlChannel, "display-name").text = data.get('GuideNumber')

	#display name
	if 'Affiliate' in data:
		ET.SubElement(xmlChannel, "display-name").text = data.get('Affiliate')

 
	if 'ImageURL' in data:
		ET.SubElement(xmlChannel, "icon", src= data.get('ImageURL'))

	maxTime = 0
		
	for program in data.get("Guide"):
		maxTime = ProcessProgram(xml,program, data.get('GuideName'))
		
	maxTime = maxTime + 1
	counter = 0

	#The first pull is for 4 hours, each of these are 8 hours
	#So if we do this 21 times we will have fetched the complete week
	try:
		while ( counter < 36 ):
			chanData = GetHdConnectChannelPrograms( deviceAuth, data.get('GuideNumber'), maxTime)
			for chan in chanData:
				for program in chan["Guide"]:
					maxTime = ProcessProgram( xml, program, data.get('GuideName'))
			counter = counter + 1

	except:
		WriteLog("It appears you do not have the HdHomeRunDvr Service.")

	

				
def saveStringToFile(strData, filename):
	with open(filename, 'wb') as outfile:
		outfile.write(strData)
					
def loadJsonFromFile(filename):
	return json.load(open(filename))

def saveJsonToFile(data, filename):
	with open(filename, 'w') as outfile:
		json.dump(data, outfile, indent=4)

def GetHdConnectDevices():
	WriteLog("Getting Connected Devices.")
	http = urllib3.PoolManager()
	discover_url_response = http.request('GET',"http://my.hdhomerun.com/discover")
	data = discover_url_response.data
	#WriteLog(data)
	obj = json.loads(data)
	return obj

def GetHdConnectDiscover(discover_url):
	WriteLog("Discovering...")
	http = urllib3.PoolManager()
	device_auth_response = http.request('GET',discover_url)
	data = device_auth_response.data
	#WriteLog(data)
	device_auth = json.loads(data)['DeviceAuth']
	return device_auth

def GetHdConnectDiscoverLineUpUrl(discover_url):
	WriteLog("Getting Lineup Url")
	http = urllib3.PoolManager()
	device_auth_response = http.request('GET',discover_url)
	data = device_auth_response.data
	LineupURL = json.loads(data)['LineupURL']
	return LineupURL

	#public class RootObject
	#{
	#    public string GuideNumber { get; set; }
	#    public string GuideName { get; set; }
	#    public string VideoCodec { get; set; }
	#    public string AudioCodec { get; set; }
	#    public int HD { get; set; }
	#    public string URL { get; set; }
	#}	

def GetHdConnectLineUp(lineupUrl):
	WriteLog("Getting Lineup")
	http = urllib3.PoolManager()
	device_auth_response = http.request('GET',lineupUrl)
	data = device_auth_response.data
	Lineup = json.loads(data)
	return Lineup


def GetHdConnectChannels(device_auth):
	WriteLog("Getting Channels.")
	http = urllib3.PoolManager()
	response = http.request('GET',"http://my.hdhomerun.com/api/guide.php?DeviceAuth=%s" % device_auth)
	data = response.data
	return json.loads(data)

def GetHdConnectChannelPrograms(device_auth, guideNumber, timeStamp):
	WriteLog("Getting Extended Programs")
	http = urllib3.PoolManager()
	response = http.request('GET',"http://my.hdhomerun.com/api/guide.php?DeviceAuth=" + device_auth +"&Channel=" + guideNumber +"&Start=" + str(timeStamp) + "&SynopsisLength=160")
	data = response.data
	return json.loads(data)	

def InList(l , value):
	if (l.count(value)>0):
		return True
	else:
		return False		
	return False

def ClearLog():
	if os.path.exists("HdHomerun.log"):
  		os.remove("HdHomerun.log")
	if os.path.exists("hdhomerun.xml"):
  		os.remove("hdhomerun.xml")	
		  		  
	

def DateTimeToEpisode(startDt, SportsCounter):

	time_now = HdHomeRunTimeStampToDate(startDt)
	season = str(int(time_now.strftime('%Y'))-1)

	if (SportsCounter != 0 ):
		episode =str( int(time_now.strftime('%m%d%H%M'))-1) + str(SportsCounter)
	else:
		episode =str( int(time_now.strftime('%m%d%H%M'))-1)
		
	return (season + "." + episode  + ". 0/1")

def DateTimeToEpisodeFriendly(startDt,SportsCounter):
	time_now = HdHomeRunTimeStampToDate(startDt)
	season = time_now.strftime('%Y')

	if (SportsCounter != 0 ):
		episode =str( int(time_now.strftime('%m%d%H%M'))) + str(SportsCounter)
	else:
		episode = time_now.strftime('%m%d%H%M')

	
	return ("S" + season + "E" + episode)

def WriteLog(message):
	timestamp = time.time()
	time_now = datetime.fromtimestamp(timestamp)
	timeString = time_now.strftime('%Y%m%d%H%M%S')

	with open ('HdHomerun.log','a') as logfile:
		output = str(timeString) + " " + str(message) + "\n"
		logfile.write(output )
	print(output.encode('utf-8'))


#this function gets the character in the ordinal position and if it isn't a-z it returns _
def getLetter(string,position):
	word = str(string).lower()
	letter = "_"
	if ( len (word) > position ):
		letter = word[position]
	
	if (letter < 'a' or letter > 'z'):
		letter = '_'
	
	return letter
	

#Deletes the cache files from the server		
def dumpCache():
	if os.path.exists("title.basics.tsv"):
  		os.remove("title.basics.tsv")	
	if os.path.exists("title.basics.tsv.gz"):
  		os.remove("title.basics.tsv.gz")	
	
	if os.path.exists("cache"):			  
		for the_file in os.listdir("cache"):
			file_path = os.path.join("cache", the_file)
			try:
				if os.path.isfile(file_path):
					os.unlink(file_path)
			except Exception as e:
				print(e)

def LoadImdb():
	WriteLog("Dumping Cache")
	#It isn't so lets dump all of the cache
	dumpCache()

	#Download the IMDB cst
	chunk_size=1024
	http = urllib3.PoolManager()
	r = http.request('GET', "https://datasets.imdbws.com/title.basics.tsv.gz", preload_content=False)

	with open("title.basics.tsv.gz", 'wb') as out:
		while True:
	 		data = r.read(chunk_size)
	 		if not data:
	 			break
	 		out.write(data)

	#Decompress the file
	with gzip.open('title.basics.tsv.gz', 'rb') as f_in:
	 	with open('title.basics.tsv', 'wb') as f_out:
	 		shutil.copyfileobj(f_in, f_out)		
	
	
	
	counter = 0
	with open('title.basics.tsv', encoding="utf8") as tsvfile:	
		reader =  csv.DictReader(tsvfile, delimiter='\t')
		for row in reader:
			counter = counter + 1
			ShowTitle = ''.join( c for c in  str(row["primaryTitle"]).lower() if  c not in "!@#$%^&&*(()_-+={}[]|\\:;<,>>?/ .`'" )

			TitleType = "series"

			if (row["titleType"] == "short"):
				TitleType = "movie"
			else:
				if (row["titleType"] == "movie"):
					TitleType = "movie"
				else:
					if (row["titleType"] == "tvMovie"):
						TitleType = "movie"
					else:
						if (row["titleType"] == "tvShort"):
							TitleType = "movie"
						else:
							if (row["titleType"] == "tvSpecial"):
								TitleType = "movie"
							else:
								if (row["titleType"] == "video"):
									continue
								else:
									if (row["titleType"] == "tvSeries"):
										TitleType = "series"
									else:
										if (row["titleType"] == "tvEpisode"):
											TitleType = "series"
										else:
											if (row["titleType"] == "tvMiniSeries"):
												TitleType = "series"	
											else:
												continue

			genres = ""
			if "genres" in row:
				genres = row["genres"]
			dTitleType = ""
			if "titleType" in row:
				dTitleType = row["titleType"]

			if (TitleType == "movie"):
				MovieList[ShowTitle] = [TitleType, genres, dTitleType ] 
			else:
				SeriesList[ShowTitle] = [TitleType, genres, dTitleType ] 
			
			# if (str( row["titleType"]) not in ITypes ):
			# 	ITypes[str( row["titleType"])] = 1
			# else:
			# 	ITypes[str( row["titleType"])]  = ITypes[str( row["titleType"])] + 1

			if ((counter % 10000)==0):
				print ("Indexed " + str(counter) + " movies.")


	
	print ("Finished indexing movies.")




def FindMovieTitle(showTitle):
	newShowTitle = ''.join( c for c in  str(showTitle).lower() if  c not in "!@#$%^&&*(()_-+={}[]|\\:;<,>>?/ .`'" 	)
	if (newShowTitle in MovieList):
		return MovieList[newShowTitle]
	return 0

def FindSeriesTitle(showTitle):
	newShowTitle = ''.join( c for c in  str(showTitle).lower() if  c not in "!@#$%^&&*(()_-+={}[]|\\:;<,>>?/ .`'" 	)
	if (newShowTitle in SeriesList):
		return SeriesList[newShowTitle]
	return 0

MovieList = {}
SeriesList = {}
SportsCounter = [0]

def GetSportsCounter():
	SportsCounter[0] = SportsCounter[0] + 1
	return SportsCounter[0]

def printIt(reader):
	for row in reader:
		return row

def main():

	SportsCounter[0] = 0
			
	WriteLog("Starting...")

	WriteLog("Clearing log files...")
	
	ClearLog()
	
	WriteLog("Downloading the IMDB databases and sorting it, this takes 30 minutes.")
	
	LoadImdb()
	
	xml = ET.Element("tv")
	
	try:
		devices = GetHdConnectDevices()
	except:
		WriteLog("No HdHomeRun devices detected.")
		exit()

	processedChannelList = ["empty","empty"]

	for device in devices: 

		if 'DeviceID' in device:

			WriteLog("Processing Device: " + device["DeviceID"])

			deviceAuth = GetHdConnectDiscover(device["DiscoverURL"])

			lineUpUrl = GetHdConnectDiscoverLineUpUrl(device["DiscoverURL"])

			LineUp = GetHdConnectLineUp(lineUpUrl)

			if ( len(LineUp) > 0):
				WriteLog("Line Up Exists for device")
				channels = GetHdConnectChannels(deviceAuth)
				for chan in channels:
					ch =str( chan.get('GuideName') )
					if (InList( processedChannelList, ch) == False):
						WriteLog ("Processing Channel: " + ch)
						processedChannelList.append(ch)
						processChannel( xml, chan, deviceAuth)
					else:
						WriteLog ("Skipping Channel " + ch + ", already processed.")
			else:
				WriteLog ("No Lineup for device!")
		else:
			WriteLog ("Must be storage...")
	
	reformed_xml = minidom.parseString(ET.tostring(xml))
	xmltv = reformed_xml.toprettyxml(encoding='utf-8')	
	WriteLog ("Finished compiling information.  Saving...")	
	saveStringToFile(xmltv, "hdhomerun.xml")
	WriteLog ("Finished.")
 
if __name__== "__main__":
  main()