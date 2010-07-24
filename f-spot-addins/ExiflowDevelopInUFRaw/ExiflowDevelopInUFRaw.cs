/*
 * ExiflowDevelopInUFRaw.cs
 *
 * Author(s)
 * 	Sebastian Berthold <exiflow@sleif.de>
 *
 * Heavily based on the original DevelopInUFRaw extension written by
 * 	Stephane Delcroix  <stephane@delcroix.org>
 *
 * Synced with: DevelopInUFRaw.cs 0.7.1
 *
 * This is free software. See COPYING for details
 */

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

using Mono.Unix;

using Hyena;
using FSpot;
using FSpot.Utils;
using FSpot.Extensions;
using FSpot.Imaging;
using FSpot.UI.Dialog;

namespace ExiflowDevelopInUFRawExtension
{
	// GUI Version
	public class ExiflowDevelopInUFRaw : AbstractExiflowDevelopInUFRaw {
		public ExiflowDevelopInUFRaw() : base("ufraw")
		{
		}

		public override void Run (object o, EventArgs e)
		{
			Log.Information ("Executing DevelopInUFRawExiflow extension");

			foreach (Photo p in App.Instance.Organizer.SelectedPhotos ()) {
				DevelopPhoto (p);
			}
		}
	}

	// Batch Version
	public class DevelopInUFRawBatchExiflow : AbstractExiflowDevelopInUFRaw {
		public DevelopInUFRawBatchExiflow() : base("ufraw-batch")
		{
		}

		public override void Run (object o, EventArgs e)
		{
			ProgressDialog pdialog = new ProgressDialog(Catalog.GetString ("Developing photos"),
														ProgressDialog.CancelButtonType.Cancel,
														App.Instance.Organizer.SelectedPhotos ().Length,
														App.Instance.Organizer.Window);
			Log.Information ("Executing DevelopInUFRawExiflow extension in batch mode");

			foreach (Photo p in App.Instance.Organizer.SelectedPhotos ()) {
				bool cancelled = pdialog.Update(String.Format(Catalog.GetString ("Developing {0}"), p.Name));
				if (cancelled) {
					break;
				}

				DevelopPhoto (p);
			}
			pdialog.Destroy();
		}
	}

	// Abstract version, contains shared functionality
	public abstract class AbstractExiflowDevelopInUFRaw : ICommand
	{
		// The executable used for developing RAWs
		private string executable;

		public const string APP_FSPOT_EXTENSION = Preferences.APP_FSPOT + "extension/";
		// We share the configuration with the original extension:
		public const string EXTENSION_DEVELOPINUFRAW = "developinufraw/";
		public const string UFRAW_JPEG_QUALITY_KEY = APP_FSPOT_EXTENSION + EXTENSION_DEVELOPINUFRAW + "ufraw_jpeg_quality";
		public const string UFRAW_ARGUMENTS_KEY = APP_FSPOT_EXTENSION + EXTENSION_DEVELOPINUFRAW + "ufraw_arguments";
		public const string UFRAW_BATCH_ARGUMENTS_KEY = APP_FSPOT_EXTENSION + EXTENSION_DEVELOPINUFRAW + "ufraw_batch_arguments";

		int ufraw_jpeg_quality;
		string ufraw_args;
		string ufraw_batch_args;

		public AbstractExiflowDevelopInUFRaw(string executable) 
		{
			this.executable = executable;
		}

		public abstract void Run (object o, EventArgs e);

		protected void DevelopPhoto (Photo p)
		{
			LoadPreference (UFRAW_JPEG_QUALITY_KEY);
			LoadPreference (UFRAW_ARGUMENTS_KEY);
			LoadPreference (UFRAW_BATCH_ARGUMENTS_KEY);

			PhotoVersion raw = p.GetVersion (Photo.OriginalVersionId) as PhotoVersion;
			if (!ImageFile.IsRaw (raw.Uri)) {
				Log.Warning ("The original version of this image is not a (supported) RAW file");
				return;
			}

			string name = GetNextVersionFileName (p);
			System.Uri developed = GetUriForVersionFileName (p, name);
			string idfile = "";


			if (ufraw_jpeg_quality < 1 || ufraw_jpeg_quality > 100) {
				Log.Debug ("Invalid JPEG quality specified, defaulting to quality 98");
				ufraw_jpeg_quality = 98;
			}

			string args = "";
			switch (executable) {
				case "ufraw":
					args += ufraw_args;
					if (GLib.FileFactory.NewForUri (Path.ChangeExtension (raw.Uri.ToString (), ".ufraw")).Exists) {
						// We found an ID file, use that instead of the raw file
						idfile = "--conf=" + GLib.Shell.Quote (Path.ChangeExtension (raw.Uri.LocalPath, ".ufraw"));
					}
					break;
				case "ufraw-batch":
					args += ufraw_batch_args;
					if (GLib.FileFactory.NewForUri (Path.Combine (FSpot.Global.BaseDirectory, "batch.ufraw")).Exists) {
						// We found an ID file, use that instead of the raw file
						idfile = "--conf=" + GLib.Shell.Quote (Path.Combine (FSpot.Global.BaseDirectory, "batch.ufraw"));
					}
					break;
			}

			args += String.Format(" --exif --overwrite --create-id=also --compression={0} --out-type=jpeg {1} --output={2} {3}", 
				ufraw_jpeg_quality,
				idfile,
				GLib.Shell.Quote (developed.LocalPath),
				GLib.Shell.Quote (raw.Uri.LocalPath));
			Log.Debug (executable + " " + args);

			System.Diagnostics.Process ufraw = System.Diagnostics.Process.Start (executable, args);
			ufraw.WaitForExit ();
			if (!(GLib.FileFactory.NewForUri (developed.ToString ())).Exists) {
				Log.Warning ("UFRaw quit with an error. Check that you have UFRaw 0.13 or newer. Or did you simply clicked on Cancel?");
				return;
			}

			if (GLib.FileFactory.NewForUri (Path.ChangeExtension (developed.ToString (), ".ufraw")).Exists) {
				// We save our own copy of the last ufraw settings, as ufraw can overwrite it's own last used settings outside f-spot
				File.Delete (Path.Combine (FSpot.Global.BaseDirectory, "batch.ufraw"));
				File.Copy (Path.ChangeExtension (developed.LocalPath, ".ufraw"), Path.Combine (FSpot.Global.BaseDirectory, "batch.ufraw"));

				// Rename the ufraw file to match the original RAW filename, instead of the (Developed In UFRaw) filename
				if (!(Path.ChangeExtension (raw.Uri.LocalPath, ".ufraw") == Path.ChangeExtension (developed.LocalPath, ".ufraw"))){
					File.Delete (Path.ChangeExtension (raw.Uri.LocalPath, ".ufraw"));
					File.Move (Path.ChangeExtension (developed.LocalPath, ".ufraw"), Path.ChangeExtension (raw.Uri.LocalPath, ".ufraw"));
				}
			}

			p.DefaultVersionId = p.AddVersion (new SafeUri (developed).GetBaseUri (),new SafeUri (developed).GetFilename (), name, true);
			p.Changes.DataChanged = true;
			App.Instance.Database.Photos.Commit (p);
		}

		private static string GetNextVersionFileName (Photo p)
		{
			return GetNextVersionFileName (p, 0);
		}

		private static string GetNextVersionFileName (Photo p, int i)
		{
			Regex exiflowpat = new Regex(@"^(\d{8}(-\d{6})?-.{3}\d{4}-.{2})(\d)(.{2})\.([^.]*$)");
			Match exiflowpatmatch = exiflowpat.Match(p.Name);
			string filename = String.Format("{0}{1}00.jpg", exiflowpatmatch.Groups[1], i, exiflowpatmatch.Groups[5]);
			System.Uri developed = GetUriForVersionFileName (p, filename);
			if (p.VersionNameExists (GetVersionName(filename)) || System.IO.File.Exists(GLib.Shell.Quote(developed.LocalPath)))
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

		private static System.Uri GetUriForVersionFileName (Photo p, string version_name)
		{
			return new System.Uri (System.IO.Path.Combine (DirectoryPath (p),  version_name ));
		}

		private static string DirectoryPath (Photo p)
		{
			return p.VersionUri (Photo.OriginalVersionId).GetBaseUri ();
		}

		void LoadPreference (string key)
		{
			switch (key) {
				case UFRAW_JPEG_QUALITY_KEY:
					ufraw_jpeg_quality = Preferences.Get<int> (key);
					break;
				case UFRAW_ARGUMENTS_KEY:
					ufraw_args = Preferences.Get<string> (key);
					break;
				case UFRAW_BATCH_ARGUMENTS_KEY:
					ufraw_batch_args = Preferences.Get<string> (key);
					break;
			}
		}

	}
}
