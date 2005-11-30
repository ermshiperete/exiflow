#!/usr/bin/env python2.4
# -*- coding: utf-8 -*-
"""
Personalize images by setting EXIF fields to values spezified in exif.cfg or
on command line. Any arbitrary field name may be configured or given.

Field names are the short versions supported by Exiftool. Look up the
Exiftool documentation for information about possible names.
"""

import os
import sys
import optparse
import subprocess
import ConfigParser
import exiflow.exif
import exiflow.filelist

def run(argv):
   configfiles = ["/etc/exiflow/exif.cfg",
                  os.path.expanduser('~/.exiflow/exif.cfg')]

   parser = optparse.OptionParser(usage="usage: %prog [options] [-- -TAGNAME=" \
                                        "VALUE [...]] <files or dirs>")
   parser.add_option("--section", "-s", dest="section",
                     help="Name of a config file section to be read. This is" \
                          " useful if different people are using the same" \
                          " camera model. By default, the section name is" \
                          " guessed from the camera model. Section 'all' is" \
                          " the default.")
   parser.add_option("-v", "--verbose", action="store_true", dest="verbose",
                     help="Be verbose.")
   options, args = parser.parse_args(argv)

   config = ConfigParser.ConfigParser()
   config.read(configfiles)

# collect args for Exiftool
   exiftool_args = ""
   remaining_args = []
   for arg in args:
      if arg.startswith("-"):
         exiftool_args += " \"" + arg +"\""
      else:
         remaining_args.append(arg)

   defaultpersonals = []
   if config.has_section("all"):
      defaultpersonals += config.items("all")
   if options.section:
      if config.has_section(options.section):
         defaultpersonals += config.items(options.section)
      else:
         sys.exit("ERROR: Section %s not found in config files" % options.section)

   filelist = exiflow.filelist.Filelist(*args)
   if options.verbose:
      print "Read config files:", " ".join(filelist.get_read_config_files())

   for filename, percentage in filelist:
      if options.verbose:
         print "%3s%% %s" % (percentage, filename)

      exif_file = exiflow.exif.Exif(filename)
      try:
         exif_file.read_exif()
      except IOError, msg:
         if options.verbose:
            print "Skipping %s: %s" % (filename, msg)
         continue

# TODO: Do we really need the colon?
      personals = defaultpersonals[:]
      if exif_file.fields.has_key("Model") and config.has_section(exif_file.fields["Model"]):
         personals += config.items(exif_file.fields["Model"])

      exif_file.fields = {}
      for key, value in personals:
         exif_file.fields[key] = value

      try:
         exif_file.write_exif()
      except IOError, msg:
         print "Error writing EXIF data:\n", filename, "\n", msg


if __name__ == "__main__":
   run(sys.argv[1:])
