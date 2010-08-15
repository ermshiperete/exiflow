/*
 * ExiflowMerge.cs
 *
 * Author(s)
 * 	Ulf Rompe <f-spot.org@rompe.org>
 *
 * Heavily based on the original RawPlusJpeg extension written by
 * 	Stephane Delcroix  <stephane@delcroix.org>
 *
 * Synced with:
 * http://svn.gnome.org/svn/f-spot/trunk/extensions/Tools/RawPlusJpeg/RawPlusJpeg.cs@4585
 *
 * This is free software. See COPYING for details
 */

using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;

using Gtk;

using FSpot;
using FSpot.Core;
using FSpot.UI.Dialog;
using FSpot.Extensions;
using FSpot.Imaging;

using Hyena;
using Hyena.Widgets;

namespace ExiflowMergeExtension
{
	public class ExiflowMerge : ICommand
	{
		public void Run (object o, EventArgs e)
		{
			Console.WriteLine ("EXECUTING EXIFLOW MERGE EXTENSION");

			if (ResponseType.Ok != HigMessageDialog.RunHigConfirmation (
				App.Instance.Organizer.Window,
				DialogFlags.DestroyWithParent,
				MessageType.Warning,
				"Merge exiflow revisions",
				"This operation will merge exiflow ( http://exiflow.sf.net/ ) revisions of the same image as one unique image. The Raw image or the jpeg with the lowest revision will be the Original version, all other versions will get the revision number and extension as their version name.\n\nNote: only enabled for some formats right now.",
				"Do it now"))
				return;

			Photo [] photos = App.Instance.Database.Photos.Query ((Tag [])null, null, null, null);
			Array.Sort (photos, new CompareName ());

			Photo previousphoto = null;

			IList<MergeRequest> merge_requests = new List<MergeRequest> ();
			ArrayList currentphotos = new ArrayList ();

			for (int i = 0; i < photos.Length; i++) {
				Photo p = photos [i];

				if (p != null && previousphoto != null && !ExiflowMatch(p, previousphoto)) {
					if (currentphotos.Count > 1) {
						merge_requests.Add (new MergeRequest (currentphotos));
					}
					currentphotos.Clear();
				}
				currentphotos.Add(p);
				previousphoto = p;
			}
			if (currentphotos.Count > 1) {
				merge_requests.Add (new MergeRequest (currentphotos));
			}

			if (merge_requests.Count == 0)
				return;

			foreach (MergeRequest mr in merge_requests)
				mr.Merge ();
			
			App.Instance.Organizer.UpdateQuery ();
		}

		private static bool ExiflowMatch (Photo p1, Photo p2)
		{
			// Filename example without time:
			// 20071231-n005678-xy000.jpg
			// Example with time:
			// 20071231-135959-n005678-xy000.jpg
			// See http://exiflow.sf.net/ for an explanation.
			String exiflowpatstring = "^\\d{8}(-\\d{6})?-.{3}\\d{4}-.{2}.{3}\\.[^.]*$";
			Regex exiflowpat = new Regex(@"^(\d{8}(-\d{6})?-.{3}\d{4}-.{2}).{3}\.[^.]*$");
			Match exiflowpatmatch1 = exiflowpat.Match(p1.Name);
			Match exiflowpatmatch2 = exiflowpat.Match(p2.Name);
                        return Regex.IsMatch (p1.Name, exiflowpatstring) &&
				Regex.IsMatch (p2.Name, exiflowpatstring) &&
				exiflowpatmatch1.Groups[1].ToString() == exiflowpatmatch2.Groups[1].ToString();
		}


		/* IComparer to sort photos by name. */
		class CompareName : IComparer
		{
			public int Compare (object obj1, object obj2)
			{
				Photo p1 = (Photo)obj1;
				Photo p2 = (Photo)obj2;
				return String.Compare (p1.Name, p2.Name);
			}
		}

		/* IComparer to sort photos by type and then by name.
		 * Raw images are always ordered before other formats. */
		class CompareNameWithRaw : IComparer
		{
			public int Compare (object obj1, object obj2)
			{
				Photo p1 = (Photo)obj1;
				Photo p2 = (Photo)obj2;
				if (ImageFile.IsRaw(p2.DefaultVersion.Uri)) {
					return 1;
				}
				return String.Compare (p1.DefaultVersion.Uri, p2.DefaultVersion.Uri);
			}
		}

		class MergeRequest 
		{
			ArrayList photos;

			public MergeRequest (ArrayList photos)
			{
				this.photos = (ArrayList) photos.Clone();
			}

			public void Merge ()
			{
				photos.Sort(new CompareNameWithRaw ());
				Console.WriteLine ("Maybe merging these photos:");
				foreach (Photo photo in this.photos) {
					Console.WriteLine (photo.Name);
				}

				Photo raw = (Photo) this.photos[0];
				foreach (Photo jpeg in this.photos.GetRange(1, this.photos.Count - 1)) {
					Console.WriteLine ("...merging {0} into {1}...", jpeg.Name, raw.Name);

					Console.WriteLine ("Merging {0} and {1}", raw.VersionUri (Photo.OriginalVersionId), jpeg.VersionUri (Photo.OriginalVersionId));
					foreach (uint version_id in jpeg.VersionIds) {
						string name = jpeg.GetVersion (version_id).Name;
						try {
							raw.DefaultVersionId = raw.CreateReparentedVersion (jpeg.GetVersion (version_id) as PhotoVersion, version_id == Photo.OriginalVersionId);
							if (version_id == Photo.OriginalVersionId)
								// Just the filename part that follows the last "-", e.g. "xy100.jpg".
								raw.RenameVersion (raw.DefaultVersionId, jpeg.Name.Substring(jpeg.Name.LastIndexOf('-') + 1));
							else
								raw.RenameVersion (raw.DefaultVersionId, name);
						} catch (Exception e) {
							Console.WriteLine (e);
						}
					}
					raw.AddTag (jpeg.Tags);
					uint [] version_ids = jpeg.VersionIds;
					Array.Reverse (version_ids);
					foreach (uint version_id in version_ids) {
						try {
							jpeg.DeleteVersion (version_id, true, true);
						} catch (Exception e) {
							Console.WriteLine (e);
						}
					}	
					raw.Changes.DataChanged = true;
					App.Instance.Database.Photos.Commit (raw);
					App.Instance.Database.Photos.Remove (jpeg);
				}
			}
		}
	}
}
