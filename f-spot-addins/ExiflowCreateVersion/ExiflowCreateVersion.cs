/*
 * ExiflowCreateVersion.cs
 *
 * Author(s)
 * 	Sebastian Berthold <exiflow@sleif.de>
 *
 * This is free software. See COPYING for details
 */

using Gnome;
using Gnome.Vfs;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System;
using System.Text.RegularExpressions;

using FSpot;
using FSpot.Extensions;
using Glade;
using Mono.Unix;

namespace ExiflowCreateVersionExtension
{
	public class ExiflowCreateVersion: ICommand
	{	
		protected string dialog_name = "exiflow_create_version_dialog";
		protected Glade.XML xml;
		private Gtk.Dialog dialog;

		[Glade.Widget] Gtk.Entry new_version_entry;
		[Glade.Widget] Gtk.Label new_filename_label;
		[Glade.Widget] Gtk.Label overwrite_warning_label;
		[Glade.Widget] Gtk.Label exiflow_schema_warning_label;
		[Glade.Widget] Gtk.Button gtk_ok;
		[Glade.Widget] Gtk.CheckButton overwrite_file_ok;
		
		string new_path;
		string new_version;
		string new_filename;
		//bool open;
		Photo currentphoto;
		//string control_file;
		Regex exiflowpat = new Regex(@"^(\d{8}(-\d{6})?-.{3}\d{4}-)(.{5}\.[^.]*)$");

		public void Run (object o, EventArgs e)
		{
			Console.WriteLine ("EXECUTING DEVELOP IN UFRawExiflow EXTENSION");
			//this.selection = selection;

			
			xml = new Glade.XML (null,"ExiflowCreateVersion.glade", dialog_name, "f-spot");
			xml.Autoconnect (this);
			dialog = (Gtk.Dialog) xml.GetWidget(dialog_name);
		foreach (Photo p in MainWindow.Toplevel.SelectedPhotos ()) {
			this.currentphoto = p;
				
				PhotoVersion raw = p.GetVersion (Photo.OriginalVersionId) as PhotoVersion;
				//if (!ImageFile.IsRaw (raw.Uri.AbsolutePath)) {
				//	Console.WriteLine ("The Original version of this image is not a (supported) RAW file");
				//	continue;
				//}
				uint default_id = p.DefaultVersionId;
				Console.WriteLine ("DefaultVersionId: "+default_id);
				string filename = GetNextVersionFileName (p);
				System.Uri developed = GetUriForVersionFileName (p, filename);
			//new_filename_entry.Text = filename;
			new_version_entry.Text = GetVersionName(filename);
				string args = String.Format("--exif --overwrite --compression=95 --out-type=jpeg --output={0} {1}", 
					CheapEscape (developed.LocalPath),
					CheapEscape (raw.Uri.ToString()));
				Console.WriteLine ("ufraw "+args);

				//System.Diagnostics.Process ufraw = System.Diagnostics.Process.Start ("ufraw", args); 
				//ufraw.WaitForExit ();
				//if (!(new Gnome.Vfs.Uri (developed.ToString ())).Exists) {
				//	Console.WriteLine ("UFraw didn't ended well. Check that you have UFRaw 0.13 (or CVS newer than 2007-09-06). Or did you simply clicked on Cancel ?");
				//	continue;
				//}

				//p.DefaultVersionId = p.AddVersion (developed, GetVersionName(filename), true);
				//Core.Database.Photos.Commit (p);

			dialog.Modal = false;
			dialog.TransientFor = null;
			
			dialog.Response += HandleResponse;

			}	
		dialog.ShowAll();
		}

		private void HandleResponse (object sender, Gtk.ResponseArgs args)
		{
			if (args.ResponseId != Gtk.ResponseType.Ok) {
				// FIXME this is to work around a bug in gtk+ where
				// the filesystem events are still listened to when
				// a FileChooserButton is destroyed but not finalized
				// and an event comes in that wants to update the child widgets.
				dialog.Destroy ();
			Console.WriteLine ("cancel pressed");
				//uri_chooser.Dispose ();
				//uri_chooser = null;
				return;
			}
			
			Console.WriteLine ("ok pressed in DEVELOP IN UFRawExiflow EXTENSION");
			new_version = new_version_entry.Text;
			new_filename = new_filename_label.Text;
			//open = open_check.Active;
			
			//command_thread = new System.Threading.Thread (new System.Threading.ThreadStart (CreateSlideshow));
			//command_thread.Name = Catalog.GetString ("Creating Slideshow");

			//progress_dialog = new FSpot.ThreadProgressDialog (command_thread, 1);
			//progress_dialog.Start ();
			CreateNewVersion();

		}

		protected void CreateNewVersion()
		{
			try {
				System.Uri original_uri = GetUriForVersionFileName (this.currentphoto, this.currentphoto.DefaultVersionUri.LocalPath);
				System.Uri new_uri = GetUriForVersionFileName (this.currentphoto, new_filename);
				Console.WriteLine ("ok pressed: old: " + this.currentphoto.DefaultVersionUri.LocalPath + "; " + original_uri.ToString() + " new: " + new_filename + "; " + new_uri.ToString());
				Xfer.XferUri (
					new Gnome.Vfs.Uri (original_uri.ToString ()), 
					new Gnome.Vfs.Uri (new_uri.ToString ()),
					XferOptions.Default, XferErrorMode.Abort, 
					XferOverwriteMode.Abort, 
					delegate (Gnome.Vfs.XferProgressInfo info) {return 1;});
				FSpot.ThumbnailGenerator.Create (new_uri).Dispose ();
				this.currentphoto.DefaultVersionId = this.currentphoto.AddVersion (new_uri, new_version_entry.Text, true);
				Core.Database.Photos.Commit (this.currentphoto);

			} finally {
				Gtk.Application.Invoke (delegate { dialog.Destroy(); });
			}
		}
		
		private void on_new_version_entry_changed(object o, EventArgs args)
		{
			Console.WriteLine ("changed filename with: " + new_version_entry.Text);
			new_filename_label.Text = GetFilenameDateAndNumberPart(this.currentphoto.Name) + new_version_entry.Text;
			if ((FileExist(this.currentphoto, new_filename_label.Text)) || (! IsExiflowSchema(new_filename_label.Text)))
			{
				gtk_ok.Sensitive=false;
			}
			else
			{
				overwrite_warning_label.Text = "";
				exiflow_schema_warning_label.Text = "";
				gtk_ok.Sensitive=true;
				overwrite_file_ok.Sensitive=false;
				overwrite_file_ok.Active=false;
			}		

			if (FileExist(this.currentphoto, new_filename_label.Text))
			{
				Console.WriteLine ("filename exists " + new_filename_label.Text);
				overwrite_warning_label.Text = "Warning: this version already exists!";
				overwrite_file_ok.Sensitive=true;
			}
			else 
			{
				//overwrite_warning_label.Text = "";
				//overwrite_file_ok.Sensitive=false;
				//overwrite_file_ok.Active=false;
			}

			if (! IsExiflowSchema(new_filename_label.Text))
			{
				Console.WriteLine ("not in exiflow schema " + new_filename_label.Text);
				//exiflow_schema_warning_label.Text = "Warning: new filename is not in the exiflow schema!";
				overwrite_warning_label.Text = "Error: new filename is not in the exiflow schema!";
			}
			else 
			{
				//exiflow_schema_warning_label.Text = "";
				//overwrite_warning_label.Text = "";
			}
		}

		private void on_overwrite_file_ok_toggled(object o , EventArgs args)
		{
			if (overwrite_file_ok.Active == true )
			{
				gtk_ok.Sensitive=true;
			}
			else
			{
				overwrite_file_ok.Sensitive=false;
				on_new_version_entry_changed(null,null);
			}
				
		}

		private string CreateExiflowFilenameForVersion(Photo p , string newversion)
		{
				Console.WriteLine ("exiflow");
				return p.Name;
			
		}
      	

		private bool IsExiflowSchema(string filename)
		{
			Regex exiflowpat = new Regex(@"^\d{8}(-\d{6})?-.{3}\d{4}-.{2}\d.{2}\.[^.]*$");
			if (exiflowpat.IsMatch(filename))
			{
				Console.WriteLine ("exiflow ok " + filename);
				return true;
			}
			else
			{
				Console.WriteLine ("exiflow not ok " + filename);
				return false;
			}
		}

		private bool VersionExist(Photo p, string newfilename)
		{
			if (p.VersionNameExists(GetVersionName(newfilename)))
				return true;
			return false;
			
		}

		private bool FileExist(Photo p, string newfilename)
		{
			System.Uri filenameuri = GetUriForVersionFileName (p, newfilename);
			if (System.IO.File.Exists(CheapEscape(filenameuri.LocalPath)))
				return true;
			return false;
			
		}

//		private static NextInExiflowSchema(string filename)
//		{
//			Regex exiflowpat = new Regex(@"^(\d{8}(-\d{6})?-.{3}\d{4}-.{2})(\d)(.{2}\.[^.]*$)");
//			Match exiflowpatmatch = exiflowpat.Match(filename);
//			string filename = String.Format("{0}{1}00.jpg", exiflowpatmatch.Groups[1], i);
//			System.Uri developed = GetUriForVersionFileName (p, filename);
//			if (p.VersionNameExists (GetVersionName(filename)) || File.Exists(CheapEscape(developed.LocalPath)))
//				return GetNextVersionFileName (p, i + 1);
//			return filename;
//			
//		}

		private static string GetNextVersionFileName (Photo p)
		{
			return GetNextVersionFileName (p, 0);
		}

		private static string GetNextVersionFileName (Photo p, int i)
		{
			Regex exiflowpat = new Regex(@"^(\d{8}(-\d{6})?-.{3}\d{4}-.{2})(\d)(.{2})\.([^.]*)$");
			Match exiflowpatmatch = exiflowpat.Match(System.IO.Path.GetFileName(p.VersionUri(p.DefaultVersionId).LocalPath));
// besser mit UnixPath.GetFileName()
			string filename = String.Format("{0}{1}00.{2}", exiflowpatmatch.Groups[1], i, exiflowpatmatch.Groups[5]);
			System.Uri developed = GetUriForVersionFileName (p, filename);
			if (p.VersionNameExists (GetVersionName(filename)) || System.IO.File.Exists(CheapEscape(developed.LocalPath)))
				return GetNextVersionFileName (p, i + 1);
			return filename;
		}

		private static string GetVersionName (string filename)
		{
			Regex exiflowpat = new Regex(@"^(\d{8}(-\d{6})?-.{3}\d{4}-)(.{5}\.[^.]*)$");
			Match exiflowpatmatch = exiflowpat.Match(filename);
			string versionname = String.Format("{0}", exiflowpatmatch.Groups[3]);
			return versionname;
		}

		private string GetFilenameDateAndNumberPart (string filename)
		{
			//Regex exiflowpat = new Regex(@"^(\d{8}(-\d{6})?-.{3}\d{4}-)(.{5}\.[^.]*)$");
			Match exiflowpatmatch = this.exiflowpat.Match(filename);
			string datenumber = String.Format("{0}", exiflowpatmatch.Groups[1]);
			return datenumber;
		}

		private static System.Uri GetUriForVersionFileName (Photo p, string version_name)
		{
			return new System.Uri (System.IO.Path.Combine (DirectoryPath (p),  version_name ));
		}

		private static string CheapEscape (string input)
		{
			string escaped = input;
			escaped = escaped.Replace (" ", "\\ ");
			escaped = escaped.Replace ("(", "\\(");
			escaped = escaped.Replace (")", "\\)");
			return escaped;
		}
		
		private static string DirectoryPath (Photo p)
		{
			System.Uri uri = p.VersionUri (Photo.OriginalVersionId);
			return uri.Scheme + "://" + uri.Host + System.IO.Path.GetDirectoryName (uri.AbsolutePath);
		}
	}
}