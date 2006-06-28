#!/usr/bin/env python2.4
# -*- coding: UTF-8 -*-
# Allow relative exiflow module imports.
# Once the package is installed they are absolute.
# Disable E0611 to supress false "No name 'xxx' in module gtk." pylint messages.
# pylint: disable-msg=E0611, W0403
"""
A nice PyGTK GUI for the exiflow tool collection.
"""
__revision__ = "$Id$"

import os.path
import sys
import logging
import optparse
import pygtk
pygtk.require("2.0")
import gtk
import gtk.glade

import exiflow.exiassign
import exiflow.exiconvert
import exiflow.exigate
import exiflow.exiimport
import exiflow.exiperson
import exiflow.exirename

gladefile = os.path.splitext(__file__)[0] + ".glade"

class WritableTextView:
   """
   Provide a file object that writes to a GTK textview.
   """
   def __init__(self, textview, color=None):
      """
      Construct the file object writing to textview, optionally using color.
      """
      self.textview = textview
      self.buffer = self.textview.get_buffer()
      self.tag_names = []
      tag_table = self.buffer.get_tag_table()
      if not tag_table.lookup("warning"):
         tag = gtk.TextTag("warning")
         tag.set_property("foreground", "red")
         tag.set_property("background", "yellow")
         tag_table.add(tag)
      if color:
         if not tag_table.lookup(color):
            tag = gtk.TextTag(color)
            tag.set_property("foreground", color)
            tag_table.add(tag)
         self.tag_names.append(color)

   def write(self, msg):		
      """
      Output msg to the textview.
      """
      my_iter = self.buffer.get_end_iter()
      tag_names = self.tag_names[:]
      if msg.startswith("WARNING") or msg.startswith("ERROR"):
         tag_names.append("warning")
      self.buffer.insert_with_tags_by_name(my_iter, msg, *tag_names)
      self.textview.scroll_mark_onscreen(self.buffer.get_insert())

   def flush(self):
      """
      Imitate a buffer flush.
      Since a textview can't be flushed, do nothing.
      """
      pass


class Directorychooser1(object):
   """
   Create a window that allows the user to select a directory.
   """
   def __init__(self, parent = None, callback=None):
      """ Instantiate the chooser. """
      self.wTree = gtk.glade.XML(gladefile, "directorychooserdialog1")
      self.window = self.wTree.get_widget("directorychooserdialog1")
      dic = {}
      for key in dir(self.__class__):
         dic[key] = getattr(self, key)
      self.wTree.signal_autoconnect(dic)
      if parent:
         self.window.set_transient_for(parent)
      self.callback = callback
      self.window.show()

   def on_directorychooserdialog1_response(self, widget, data = None):
      """ Callback function for the chooser. """
      if data == gtk.RESPONSE_OK and callable(self.callback):
         self.callback(widget.get_filename())
      self.window.destroy()

class Filechooser1(object):
   """
   Create a window that allows the user to select files.
   """
   def __init__(self, parent = None, callback=None):
      """ Instantiate the chooser. """
      self.wTree = gtk.glade.XML(gladefile, "filechooserdialog1")
      self.window = self.wTree.get_widget("filechooserdialog1")
      dic = {}
      for key in dir(self.__class__):
         dic[key] = getattr(self, key)
      self.wTree.signal_autoconnect(dic)
      if parent:
         self.window.set_transient_for(parent)
      self.callback = callback
      self.window.show()

   def on_filechooserdialog1_response(self, widget, data = None):
      """ Callback function for the chooser. """
      if data == gtk.RESPONSE_OK and callable(self.callback):
         self.callback(widget.get_filenames())
      self.window.destroy()


class Aboutdialog1(object):
   """
   Pop up a window that gives some basic information about the program.
   """
   def __init__(self, parent = None):
      """ Instantiate the dialog. """
      self.wTree = gtk.glade.XML(gladefile, "aboutdialog1")
      self.window = self.wTree.get_widget("aboutdialog1")
      dic = {}
      for key in dir(self.__class__):
         dic[key] = getattr(self, key)
      self.wTree.signal_autoconnect(dic)
      if parent:
         self.window.set_transient_for(parent)
      self.window.show()


class Window1(object):
   """
   The program's main window.
   """
   def __init__(self):
      """ Instantiate the main window. """
      self._cancelled = False
      self.wTree = gtk.glade.XML(gladefile, "mainwindow")
      self.window = self.wTree.get_widget("mainwindow")
# Initialize treeview
      treeview = self.wTree.get_widget("treeview1")
      self.liststore = gtk.ListStore(str)
      treeview.set_model(self.liststore)
      text_cell = gtk.CellRendererText()
      text_column = gtk.TreeViewColumn("Filename")
      text_column.pack_start(text_cell, True)
      text_column.add_attribute(text_cell, "text", 0)
      #text_column.set_attributes(text_cell, markup=1)
      #wir müssen markup anschalten um den text später formatieren zu können
      treeview.append_column(text_column)
      dic = {}
      for key in dir(self.__class__):
         dic[key] = getattr(self, key)
      self.wTree.signal_autoconnect(dic)
      self.window.show()
# Create TextView and use it
      sys.stdout = WritableTextView(self.wTree.get_widget("textview1"))
      sys.stderr = WritableTextView(self.wTree.get_widget("textview1"), "blue")
      stdlog = WritableTextView(self.wTree.get_widget("textview1"), "red")
      logging.getLogger().addHandler(logging.StreamHandler(stdlog))

   def _make_sensitive(self, name):
      """ Set widget called name sensitive. """
      self.wTree.get_widget(name).set_sensitive(True)

   def _make_insensitive(self, name):
      """ Set widget called name insensitive. """
      self.wTree.get_widget(name).set_sensitive(False)

   def _is_active(self, name):
      """ Retrun True if widget "name" is activated, False otherwise. """
      return self.wTree.get_widget(name).get_active()

   def set_text(self, name, text):
      """ Set text of widget "name" to text. """
      self.wTree.get_widget(name).set_text(text)

   def on_button_open_clicked(self, *dummy):
      """ Callback for the "open" button. """
      dummy = Filechooser1(self.window, self.set_filelist)

   def on_button_exiimport_browse_importdir_clicked(self, *dummy):
      """ Callback for the exiimport's "browse importdir" button. """
      dummy = Directorychooser1(self.window,
               self.wTree.get_widget("exiimport_importdir_entry").set_text)

   def on_button_exiimport_browse_targetdir_clicked(self, *dummy):
      """ Callback for the exiimport's "browse targetdir" button. """
      dummy = Directorychooser1(self.window,
               self.wTree.get_widget("exiimport_targetdir_entry").set_text)

   def on_info1_activate(self, *dummy):
      """ Callback for the "about" menu entry. """
      dummy = Aboutdialog1(self.window)

   def on_mainwindow_destroy(self, *dummy):
      """ Callback for the window's close button. """
      gtk.main_quit()

   def set_filelist(self, files):
      """ Put files into the filelist. """
      logger = logging.getLogger("exigui.set_filelist")
      self.liststore.clear()
      for filename in files:
         filename = os.path.abspath(filename)
         if os.path.exists(filename):
            self.liststore.append([filename])
         else:
            logger.warning("%s doesn't exist!", filename)

   def on_exirename_camid_auto_activate(self, *dummy):
      """ Callback for exirename's "auto cam_id" selection. """
      self._make_insensitive("exirename_cam_id_entry")

   def on_exirename_camid_custom_activate(self, *dummy):
      """ Callback for exirename's "custom cam_id" selection. """
      self._make_sensitive("exirename_cam_id_entry")

   def on_exirename_artist_auto_activate(self, *dummy):
      """ Callback for exirename's "auto artist" selection. """
      self._make_insensitive("exirename_artist_initials_entry")

   def on_exirename_artist_custom_activate(self, *dummy):
      """ Callback for exirename's "custom artist" selection. """
      self._make_sensitive("exirename_artist_initials_entry")

   def on_exiperson_section_auto_activate(self, *dummy):
      """ Callback for exirename's "auto section" selection. """
      self._make_insensitive("exiperson_section_entry")

   def on_exiperson_section_custom_activate(self, *dummy):
      """ Callback for exirename's "custom section" selection. """
      self._make_sensitive("exiperson_section_entry")

   def _progress_callback(self, filename, newname, percentage,
                          keep_original=False):
      """
      This callback is given as a callable to the main programs and is
      called after each processed file. filename and newname may of course
      be the same. If keep_original is True, add newname instead of replacing
      filename with it.
      Return self._cancelled which is True after Cancel has been pressed.
      """
      if filename != newname:
         if keep_original:
            self.liststore.append([newname])
         else:
            for rownum in range(0, len(self.liststore)):
               if self.liststore[rownum][0] == filename:
                  self.liststore[rownum][0] = newname
      progressbar = self.wTree.get_widget("progressbar1")
      progressbar.set_fraction(float(percentage) / 100)
      progressbar.set_text(u"%s %%" % percentage)
      while gtk.events_pending():
         gtk.main_iteration(False)
      return self._cancelled
      
   def on_cancel_activate(self, widget, *dummy):
      """
      Called from the cancel button.
      """
      logger = logging.getLogger("exigui.on_cancel_activate")
      self._cancelled = True
      widget.set_sensitive(False)
      logger.warning("CANCELLED!")

   def on_run_activate(self, widget, *dummy):
      """
      Called from the run button.
      """
      logger = logging.getLogger("exigui.on_run_activate")
      cancel_button = self.wTree.get_widget("cancel_button")
      cancel_button.set_sensitive(True)
      nbook = self.wTree.get_widget("notebook1")
      nbook.set_sensitive(False)
      widget.set_sensitive(False)
      self._cancelled = False
      
      ntab = nbook.get_tab_label(nbook.get_nth_page(nbook.get_current_page()))
      label = ntab.get_text()
      logger.warning("Running %s", label)
      method = getattr(self, "run_" + label.replace(" ", "_"))
      method()
      
      nbook.set_sensitive(True)
      cancel_button.set_sensitive(False)
      widget.set_sensitive(True)

   def run_exiimport(self):
      """ Run exiimport. """
      logger = logging.getLogger("exigui.run_exiimport")
      args = ["-v"]
      import_dir = self.wTree.get_widget("exiimport_importdir_entry")
      device = self.wTree.get_widget("exiimport_device_entry")
      target_dir = self.wTree.get_widget("exiimport_targetdir_entry")
      if import_dir.get_text():
         args.append("--mount=" + import_dir.get_text())
      if device.get_text():
         args.append("--device=" + device.get_text())
      if target_dir.get_text():
         args.append("--target=" + target_dir.get_text())
      try:
         exiflow.exiimport.run(args, self._progress_callback)
      except IOError, msg:
         logger.error("ERROR: %s", msg)

   def run_exirename(self):
      """ Run exirename. """
      logger = logging.getLogger("exigui.run_exirename")
      args = ["-v"]
      artist_initials = self.wTree.get_widget("exirename_artist_initials_entry")
      cam_id = self.wTree.get_widget("exirename_cam_id_entry")
      if self._is_active("exirename_artist_initials_entry_button_custom"):
         args.append("--artist_initials=" + artist_initials.get_text())
      if self._is_active("exirename_cam_id_entry_button_custom"):
         args.append("--cam_id=" + cam_id.get_text())
      args += [entry[0] for entry in self.liststore]
      try:
         exiflow.exirename.run(args, self._progress_callback)
      except IOError, msg:
         logger.error("ERROR: %s", msg)

   def run_exiperson(self):
      """ Run exiperson. """
      logger = logging.getLogger("exigui.run_exiperson")
      args = ["-v"]
      exif_section = self.wTree.get_widget("exiperson_section_entry")
      if self._is_active("exiperson_section_entry_button_custom"):
         args.append("--section=" + exif_section.get_text())
      args += [entry[0] for entry in self.liststore]
      try:
         exiflow.exiperson.run(args, self._progress_callback)
      except IOError, msg:
         logger.error("ERROR: %s", msg)
      
   def run_exiconvert(self):
      """ Run exiconvert. """
      logger = logging.getLogger("exigui.run_exiconvert")
      args = ["-v"]
      args += [entry[0] for entry in self.liststore]
      try:
         exiflow.exiconvert.run(args, self._progress_callback)
      except IOError, msg:
         logger.error("ERROR: %s", msg)

   def run_exiassign(self):
      """ Run exiassign. """
      logger = logging.getLogger("exigui.run_exiassign")
      args = ["-v"]
      if self._is_active("exiassign_force_checkbutton"):
         args.append("--force")
      args += [entry[0] for entry in self.liststore]
      try:
         exiflow.exiassign.run(args, self._progress_callback)
      except IOError, msg:
         logger.error("ERROR: %s", msg)

   def run_exigate_gthumb(self):
      """ Run exigate. """
      logger = logging.getLogger("exigui.run_exigate_gthumb")
      if self._is_active("exigate_gthumb_nooptions"):
         args = ["-v"]
      if self._is_active("exigate_gthumb_addfields"):
         args = ["--additional-fields"]
      if self._is_active("exigate_gthumb_template"):
         args = ["--template"]
      if self._is_active("exigate_gthumb_cleanup"):
         args = ["--cleanup"]
      args.append("-v")
      args += [entry[0] for entry in self.liststore]
      try:
         exiflow.exigate.run(args, self._progress_callback)
      except IOError, msg:
         logger.error("ERROR: %s", msg)


def run(argv):
   """
   Take an equivalent of sys.argv[1:] and run the GUI.
   """
   parser = optparse.OptionParser()
   parser.add_option("-m", "--mount", dest="mount",
                     help="Mountpoint of directory to import. Corresponds"
                          " to %m in the gnome-volume-manager config dialog.")
   parser.add_option("-t", "--target", dest="target",
                     help="Target directory. A subdirectory will be created"
                          " in this directory.")
   parser.add_option("-d", "--device", dest="device",
                     help="Mounted device file. If given, this device will be "
                          "unmounted using pumount after the import. "
                          "Corresponds to %d in the gnome-volume-manager "
                          "config dialog.")
   parser.add_option("-v", "--verbose", action="store_true", dest="verbose",
                     help="Be verbose.")
   options, args = parser.parse_args(argv)
   logging.basicConfig(format="%(module)s: %(message)s", level=logging.INFO)
   if options.verbose:
      logging.getLogger("exigui").setLevel(logging.INFO)

   win1 = Window1()
   if options.mount:
      win1.set_text("exiimport_importdir_entry", options.mount)
   if options.device:
      win1.set_text("exiimport_device_entry", options.device)
   if options.target:
      win1.set_text("exiimport_targetdir_entry", options.target)
   if len(args) > 0:
      win1.set_filelist(args)
   gtk.main()
   return 0


if __name__ == "__main__":
   run(sys.argv[1:]) 

