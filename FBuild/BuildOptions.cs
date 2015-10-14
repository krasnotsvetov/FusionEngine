﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using Fusion;
using Fusion.Core.Shell;
using System.Diagnostics;

namespace FBuild {

	public class BuildOptions {

		/// <summary>
		/// Input directory
		/// </summary>
		[CommandLineParser.Name("in", "Input directory")]
		public string InputDirectory { get; set; }
			
		/// <summary>
		/// Output directory
		/// </summary>
		[CommandLineParser.Name("out", "Output directory")]
		public string OutputDirectory { get; set; }
			
		/// <summary>
		/// Temporary directory
		/// </summary>
		[CommandLineParser.Name("temp", "Temporary directory")]
		public string TempDirectory { get; set; }
			
		/// <summary>
		/// Force rebuild
		/// </summary>
		[CommandLineParser.Name("force", "Force rebuild")]
		public bool ForceRebuild { get; set; }
			

		/// <summary>
		/// Full input directory
		/// </summary>
		public string FullInputDirectory { 
			get {
				return Path.GetFullPath( InputDirectory );
			}
		}

		/// <summary>
		/// Full output directory
		/// </summary>
		public string FullOutputDirectory { 
			get {
				return Path.GetFullPath( OutputDirectory );
			}
		}

		/// <summary>
		/// Full temp directory
		/// </summary>
		public string FullTempDirectory { 
			get {
				return Path.GetFullPath( TempDirectory );
			}
		}


		public string ContentIniFile {
			get {
				return Path.Combine( FullInputDirectory, ".content" );
			}
		}


		/// <summary>
		/// Ctor
		/// </summary>
		public BuildOptions ()
		{
		}



		/// <summary>
		/// Checks options
		/// </summary>
		public void CheckOptionsAndMakeDirs ()
		{
			if ( InputDirectory==null ) {
				throw new BuildException("Input directory is not specified (/in:)");
			}
			if ( OutputDirectory==null ) {
				throw new BuildException("Output directory is not specified (/out:)");
			}
			if ( TempDirectory==null ) {
				throw new BuildException("Temporary directory is not specified (/temp:)");
			}


			if ( !Directory.Exists(FullTempDirectory) ) {
				var dir = Directory.CreateDirectory( FullTempDirectory );
				dir.Attributes = FileAttributes.Hidden;
			}

			if ( !Directory.Exists(FullOutputDirectory) ) {
				var dir = Directory.CreateDirectory( FullOutputDirectory );
			}

			if ( !Directory.Exists(FullInputDirectory) ) {
				throw new BuildException("Input directory does not exist");
			}

			if ( !File.Exists(ContentIniFile) ) {
				throw new BuildException("File '.content' not found");
			}
				

		}
	}


}
