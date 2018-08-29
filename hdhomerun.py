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

def ProcessProgram(xml, program, guideName):

	WriteLog ("Processing Show: " + program['Title'])

	timezone_offset = get_utc_offset_str().replace(":","")
	#program
	#Create the "programme" element and set the Channel attribute to "GuideName" from json
	xmlProgram = ET.SubElement(xml, "programme", channel = guideName)
	#	 channel=channel['GuideName'])

	#set the start date and time from the feed
	xmlProgram.set("start", datetime.fromtimestamp(program['StartTime']).strftime('%Y%m%d%H%M%S') + " " + timezone_offset)

	#set the end date and time from the feed
	xmlProgram.set("stop", datetime.fromtimestamp(program['EndTime']).strftime('%Y%m%d%H%M%S') + " " + timezone_offset)

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

	addedEpisode = False

	if 'EpisodeNumber' in program:
		#add the friendly display
		ET.SubElement(xmlProgram, "episode-num", system="onscreen").text = program['EpisodeNumber']
		#Fake the xml version
		en = program['EpisodeNumber']
		parts = en.split("E")
		season = parts[0].replace("S","")
		episode = parts[1]
		#Assign the fake xml version
		ET.SubElement(xmlProgram, "episode-num", system="xmltv_ns").text = (season + " . " + episode  + " . 0/1")
		#set the category flag to series
		ET.SubElement(xmlProgram, "category", lang="en" ).text = "series"
		addedEpisode = True

	if 'OriginalAirdate' in program:
		if program['OriginalAirdate'] > 0:
			#The 86400 is because the HdHomeRun feed is off by a day, this fixes that.
			ET.SubElement(xmlProgram, "previously-shown", start = datetime.fromtimestamp(program['OriginalAirdate'] + 86400 ).strftime('%Y%m%d%H%M%S') + " " + timezone_offset)
				
	if 'ImageURL' in program:
		ET.SubElement(xmlProgram, "icon", src=program['ImageURL'])

	xmlAudio = ET.SubElement(xmlProgram,"audio")
	ET.SubElement( xmlAudio, "stereo").text = "stereo"
	ET.SubElement(xmlProgram, "subtitles", type="teletext")		

	addedType = False

	imdbData = -1

	if 'filter' in program:

		for filter in program['Filter']:
			
			filterstringLower = str(filter).lower()
			
			#Does HdHomeRun think it is a movie?
			if (filterstringLower=="movies"):
				#Yes, try to find the movie in the database	
				imdbData = FindTitle(program['Title'])
				
				#Does the movie not exist in the IMDB database?
				if ( imdbData == 0 ):
					#Have we added a fake episode yet?
					if (addedEpisode == False):
						#Ok, we will flag it as a series to get it out of movies
						ET.SubElement(xmlProgram, "category",lang="en").text = "series"
						#Add fake series/episode info
						ET.SubElement(xmlProgram, "episode-num", system="xmltv_ns").text = DateTimeToEpisode()
						ET.SubElement(xmlProgram, "episode-num", system="onscreen").text = DateTimeToEpisodeFriendly()
						addedType= True
					continue
				else:
					#Does the Imdb say it's a movie?
					if ( imdbData[1] == "movie" ):
						#yes, ok, add the tag
						ET.SubElement(xmlProgram, "category",lang="en").text = "movies"
						addedType=True
						continue
					else:
						#Set the type to what the IMDB says
						ET.SubElement(xmlProgram, "category",lang="en").text = str(imdbData[1]).lower()
						addedType=True
						#Have we added a fake episode yet?
						if (addedEpisode == False):
							ET.SubElement(xmlProgram, "category",lang="en").text = "series"
							ET.SubElement(xmlProgram, "episode-num", system="xmltv_ns").text = DateTimeToEpisode()
							ET.SubElement(xmlProgram, "episode-num", system="onscreen").text = DateTimeToEpisodeFriendly()
			else:

				#ok, just add whatever the category is to the record.
				ET.SubElement(xmlProgram, "category",lang="en").text = filterstringLower

	if ( addedType == False ):
		imdbData = FindTitle(program['Title'])
		if (imdbData==0):
			words = str(program['Title']).split()
			if 'News' in words :
				ET.SubElement(xmlProgram, "category",lang="en").text = "news"
			else:
				if	('Sports' in words):
					ET.SubElement(xmlProgram, "category",lang="en").text = "sports"
				else:
					if ('Football' in words):
						ET.SubElement(xmlProgram, "category",lang="en").text = "sports"
					else:
						if ('Soccer' in words):
							ET.SubElement(xmlProgram, "category",lang="en").text = "sports"
						else:
							if ('Baseball' in words):
								ET.SubElement(xmlProgram, "category",lang="en").text = "sports"
							else:
								if ('Dance' in words):
									ET.SubElement(xmlProgram, "category",lang="en").text = "sports"
								else:
									if ('Dancing' in words):
										ET.SubElement(xmlProgram, "category",lang="en").text = "sports"
									else:
										if ('Olympics' in words):
											ET.SubElement(xmlProgram, "category",lang="en").text = "sports"
										else:
											if ('Cycling' in words):
												ET.SubElement(xmlProgram, "category",lang="en").text = "sports"
											else:
												if ('Billiards' in words):
													ET.SubElement(xmlProgram, "category",lang="en").text = "sports"
												else:
													if ('BasketBall' in words):
														ET.SubElement(xmlProgram, "category",lang="en").text = "sports"
													else:
														if ('Athletics' in words):
															ET.SubElement(xmlProgram, "category",lang="en").text = "sports"
														else:
															if ('Boxing' in words):
																ET.SubElement(xmlProgram, "category",lang="en").text = "sports"
															else:
																if ('Cricket' in words):
																	ET.SubElement(xmlProgram, "category",lang="en").text = "sports"
																else:
																	if ('Fencing' in words): 
																		ET.SubElement(xmlProgram, "category",lang="en").text = "sports"
																	else:
																		ET.SubElement(xmlProgram, "category",lang="en").text = "series"
			ET.SubElement(xmlProgram, "episode-num", system="xmltv_ns").text = DateTimeToEpisode()
			ET.SubElement(xmlProgram, "episode-num", system="onscreen").text = DateTimeToEpisodeFriendly()
		else:
			ET.SubElement(xmlProgram, "category",lang="en").text = imdbData[1]
			ET.SubElement(xmlProgram, "episode-num", system="xmltv_ns").text = DateTimeToEpisode()
			ET.SubElement(xmlProgram, "episode-num", system="onscreen").text = DateTimeToEpisodeFriendly()

	if imdbData == -1:
		imdbData = FindTitle(program['Title'])
	
	if (imdbData != 0):
		words = str(imdbData[8]).split(',')
		for word in words:
			if (str(word).strip()):
				ET.SubElement(xmlProgram, "category",lang="en").text = word.lower()


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
		while ( counter < 24 ):
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
	#WriteLog(data)
	return json.loads(data)

def GetHdConnectChannelPrograms(device_auth, guideNumber, timeStamp):
	WriteLog("Getting Extended Programs")
	http = urllib3.PoolManager()
	response = http.request('GET',"http://my.hdhomerun.com/api/guide.php?DeviceAuth=" + device_auth +"&Channel=" + guideNumber +"&Start=" + str(timeStamp) + "&SynopsisLength=160")
	data = response.data
	#WriteLog(data)
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
		  		  
	

def DateTimeToEpisode():
	timestamp = time.time()
	time_now = datetime.fromtimestamp(timestamp)
	season = time_now.strftime('%Y')
	episode = time_now.strftime('%m%d%H')
	return (season + " . " + episode  + " . 0/1")

def DateTimeToEpisodeFriendly():
	timestamp = time.time()
	time_now = datetime.fromtimestamp(timestamp)
	season = time_now.strftime('%Y')
	episode = time_now.strftime('%m%d%H')
	return ("S" + season + "E" + episode)

def WriteLog(message):
	timestamp = time.time()
	time_now = datetime.fromtimestamp(timestamp)
	timeString = time_now.strftime('%Y%m%d%H%M%S')

	with open ('HdHomerun.log','a') as logfile:
		output = str(timeString) + " " + str(message) + "\n"
		logfile.write(output )
	print(output.encode('utf-8'))

def WriteCacheDate():
	if not os.path.exists("cache"):
		os.makedirs("cache")
	timestamp = time.time()
	time_now = datetime.fromtimestamp(timestamp)
	timeString = time_now.strftime('%Y%m')
	
	with open ("cache/" + timeString + ".txt",'w') as logfile:
		logfile.write("")

def getLetter(string,position):
	word = str(string).lower()
	letter = "_"
	if ( len (word) > position ):
		letter = word[position]
	
	if (letter < 'a' or letter > 'z'):
		letter = '_'
	
	return letter
	
def WriteDb(row):

	if not os.path.exists("cache"):
		os.makedirs("cache")

	L1 = getLetter( row[2] , 0 )
	L2 = getLetter( row[2] , 3 )
	L3 = getLetter( row[2] , 5 )
	#L4 = getLetter( row[2] , 3 )
	L4 = "_"
	#L5 = getLetter( row[2] , 4 )
	L5 = "_"

	filename = "cache/title.basics." + L1 + L2 + L3 + L4 + L5 + ".tsv"

	with open (filename,'a',newline='\n') as tsvfile:
		spamwriter = csv.writer(tsvfile, delimiter='\t', quotechar='|', quoting=csv.QUOTE_MINIMAL)
		spamwriter.writerow(row)
		
def dumpCache():
	if os.path.exists("title.basics.tsv"):
  		os.remove("title.basics.tsv")	
	if os.path.exists("title.basics.tsv.gz"):
  		os.remove("title.basics.tsv.gz")	
	for the_file in os.listdir("cache"):
		file_path = os.path.join("cache", the_file)
		try:
			if os.path.isfile(file_path):
				os.unlink(file_path)
		except Exception as e:
			print(e)

def LoadImdb():
	if not os.path.exists("cache"):
		os.makedirs("cache")

	timestamp = time.time()
	time_now = datetime.fromtimestamp(timestamp)
	timeString = time_now.strftime('%Y%m')


	if os.path.exists("cache/" + timeString + ".txt"):
		#We have already loaded the database for the day, no need to reload.
		return

	WriteLog("Dumping Cache")

	dumpCache()

	chunk_size=1024
	http = urllib3.PoolManager()
	r = http.request('GET', "https://datasets.imdbws.com/title.basics.tsv.gz", preload_content=False)

	with open("title.basics.tsv.gz", 'wb') as out:
		while True:
	 		data = r.read(chunk_size)
	 		if not data:
	 			break
	 		out.write(data)

	with gzip.open('title.basics.tsv.gz', 'rb') as f_in:
	 	with open('title.basics.tsv', 'wb') as f_out:
	 		shutil.copyfileobj(f_in, f_out)		
	
	with open('title.basics.tsv', encoding="utf8") as tsvfile:
		reader = csv.reader(tsvfile, delimiter='\t')
		for row in reader:
			WriteDb(row)
	
	WriteCacheDate()
	


def FindTitle(showTitle):
	
	L1 = getLetter( showTitle , 0 )
	L2 = getLetter( showTitle , 3 )
	L3 = getLetter( showTitle , 5 )
	# L4 = getLetter( showTitle , 3 )
	L4 = "_"
	# L5 = getLetter( showTitle , 4 )
	L5 = "_"

	filename = "cache/title.basics." + L1 + L2 + L3 + L4 + L5 + ".tsv"

	if os.path.exists(filename):
		with open(filename,  encoding = "ISO-8859-1") as tsvfile:
			reader = csv.reader(tsvfile, delimiter='\t')
			for row in reader:
				if row[2] == showTitle or row[3]==showTitle:
					return row
	return 0


def main():

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