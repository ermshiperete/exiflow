#!/usr/bin/env python2.4
# -*- coding: utf-8 -*-
# Allow relative exiflow module imports.
# Once the package is installed they are absolute.
# pylint: disable-msg=W0403
"""
Import files from given directories to your photo folder.
Optionally unmounts source media after successfull import.
"""
__revision__ = "$Id$"

import os
import sys
import stat
import shutil
import logging
import optparse
import subprocess
import exiflow.filelist

def run(argv, callback=None):
   """
   Take an equivalent of sys.argv[1:] and optionally a callable.
   Parse options, import files and optionally call the callable on every
   processed file with 3 arguments: filename, newname, percentage.
   If the callable returns True, stop the processing.
   """
# Parse command line.
   parser = optparse.OptionParser()
   parser.add_option("-m", "--mount", dest="mount",
                     help="Mountpoint of directory to import. Corresponds"
                          " to %m in the gnome-volume-manager config dialog.")
   parser.add_option("-t", "--target", dest="target",
                     help="Target directory. A subdirectory will be created"
                          " in this directory.")
   parser.add_option("-d", "--device", dest="device",
                     help="Mounted device file. If given, this device will be"
                          " unmounted using pumount after the import."
                          " Corresponds to %d in the gnome-volume-manager"
                          " config dialog.")
   parser.add_option("-v", "--verbose", action="store_true", dest="verbose",
                     help="Be verbose.")
   options, args = parser.parse_args(argv)
   logging.basicConfig(format="%(module)s: %(message)s")
   if options.verbose:
      logging.getLogger().setLevel(logging.INFO)
   logger = logging.getLogger("exiimport")
   if len(args) > 0 or not options.mount or not options.target:
      logger.error("Wrong syntax.")
      parser.print_help()
      sys.exit(1)

# Build file list whithout skipping unknown files
   filelist = exiflow.filelist.Filelist([])
   filelist.process_unknown_types()
   filelist.add_files([options.mount])

# Cry if we found no images
   if filelist.get_filecount() == 0:
      logger.error("No files to import, sorry.")
      sys.exit(1)

# Create targetdir
   targetdir = os.path.join(options.target, filelist.get_daterange())
# TODO: find a better solution than just appendings "+" chars.
   while os.path.exists(targetdir):
      targetdir += "+"
   os.makedirs(targetdir)
   logger.warning("Importing %s MB in %s files to %s",
                  filelist.get_fullsize() / 1024 / 1024,
                  filelist.get_filecount(), targetdir)

# Copy files
   for filename, percentage in filelist:
      logger.info("%3s%% %s", percentage, filename)
      if callable(callback):
         if callback("", os.path.join(targetdir, os.path.basename(filename)),
                                      percentage, keep_original=True):
            break

      shutil.copy2(filename, targetdir)
      os.chmod(os.path.join(targetdir, os.path.basename(filename)),
               stat.S_IMODE(0644))

# Unmount card
   if options.device:
      subprocess.call("pumount " + options.device, shell=True)


if __name__ == "__main__":
   run(sys.argv[1:])

