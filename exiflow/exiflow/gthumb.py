#!/usr/bin/env python2.4
# -*- coding: utf-8 -*-
"""
A class that handles reading and writing gthumb comment files for an image file.
"""

import os
import re
import sys
import gzip
import xml.dom.minidom

class Gthumb:

   def __init__(self, filename):
      """
      Create a Gthumb object for image filename.
      """
      self.fields = {}
      self.filename = os.path.abspath(filename)
      self.commentsdir = os.path.join(os.path.dirname(self.filename),
                                      ".comments")
      self.commentsfile = os.path.join(commentsdir,
                                       os.path.basename(self.filename) + ".xml")


   def read(self):
      """
      Read gthumb comment information for filename and fill the dictionary
      self.fields with the corresponding EXIF values.
      Return True on success and False if no comment file exists.

      Translation of Exif values to gthumb sections:

          "Artist"            => "Note" line: "^Artist::.*"
          "Credit"            => "Note" line: "^Credit::.*"
          "Copyright"         => "Note" line: "^Copyright::.*"
          "CopyrightNotice"   => "Note" line: "^CopyrightNotice::.*"
          "UserComment"       => "Note" line: "^UserComment::.*"
          "ImageDescription"  => "Note" free text without "^\w+::"
          "Keywords"          => "Keywords" comma separated list
          "Location"          => "Place"
          "DateTimeOriginal"  => "Time" in seconds since 1970-01-01
          "XPTitle"           => First line of "Note", if any

      In fact, any line of "Note" that consists of a keyword followed by
      two colons and some random text is converted into it's EXIF equivalent.
      """
      if os.path.isfile(self.commentsfile):
         mydata = {}
         mydom = xml.dom.minidom.parse(gzip.open(myxmlfile))
         for field in ("Note", "Keywords", "Place", "Time"):
            mynodes = mydom.getElementsByTagName(field)
            if len(mynodes) > 0:
               mynodes = mynodes[0].childNodes
               if len(mynodes) > 0:
                  mydata[field] = mynodes[0].wholeText

         if "Time" in mydata:
            self.fields["DateTimeOriginal"] = mydata["Time"]
         if "Place" in mydata:
            self.fields["Location"] = mydata["Place"]
         if "Keywords" in mydata:
            self.fields["Keywords"] = mydata["Keywords"]
         if "Note" in mydata:
            note = []
            myregex = re.compile("(\w+)::(.+)$")
            for line in mydata["Note"].split("\n"):
               mymatch = myregex.match(line)
               if mymatch:
                  self.fields[mymatch.group(1)] = mymatch.group(2).strip()
               else:
                  note.append(line)
            if len(note) > 0:
               self.fields["XPTitle"] = note[0]
               self.fields["ImageDescription"] = "\n".join(note).strip()
         return True
      else:
         return False


   def write(self, myaddfields=False, mytemplate=False):
      """
      Write Exif information from self.fields into gthumb comment file.
      Apply reverse mapping of self.read().

      myaddfields: Write some additional fields.
      mytemplate: Write empty fields as well.
      """
      exiffields = ["Artist", "Credit", "Copyright", "CopyrightNotice", "UserComment"]
      filename = os.path.abspath(filename)
      commentsdir = os.path.join(os.path.dirname(filename), ".comments")
      myxmlfile = os.path.join(commentsdir, os.path.basename(filename) + ".xml")
      mydata = {}
      mydata["Place"] = self.fields.get("Location", "")
      mydata["Time"] = self.fields.get("DateTimeOriginal", "")
      mydata["Note"] = self.fields.get("ImageDescription", "")
      tmpkeywords = []
      for keyword in self.fields.get("Keywords", "").split(","):
         tmpkeywords.append(keyword.strip())
      mydata["Keywords"] = ",".join(tmpkeywords)

# Add XPTitle to Note if Note doesn't contain it already
      mytitle = self.fields.get("XPTitle", "")
      if len(mytitle) > 0 and not mytitle in mydata["Note"].splitlines():
         mydata["Note"] = self.fields["XPTitle"] + "\n" + mydata["Note"]
      if myaddfields or mytemplate:
         mydata["Note"] += "\n"
         for field in exiffields:
            fieldvalue = self.fields.get(field, "")
            if mytemplate or len(fieldvalue) > 0:
               mydata["Note"] += "\n" + field + ":: " + fieldvalue

      mydom = xml.dom.minidom.Document()
      comment = mydom.createElement("Comment")
      comment.setAttribute("format", "2.0")
      mydom.appendChild(comment)

      for name in ("Place", "Time", "Note", "Keywords"):
         element = mydom.createElement(name)
         text = mydom.createTextNode(mydata[name])
         element.appendChild(text)
         comment.appendChild(element)

      if not os.path.isdir(commentsdir):
         os.makedirs(commentsdir)
      gzip.open(myxmlfile, "wb").write(mydom.toxml(encoding="utf-8"))


   def get_mtime(self):
      """
      Get modification time of comment file or 0 if it doesn't exist.
      """
      if os.path.isfile(self.commentsfile):
         return os.path.getmtime(self.commentsfile)
      else:
         return 0


   def set_mtime(self, mtime)
      """
      Set modification time of comment file to mtime.
      Returns True on success and False if no comment file exists.
      """
      if os.path.isfile(self.commentsfile):
         os.utime(self.commentsfile, (mtime, mtime))
         return True
      else:
         return False

