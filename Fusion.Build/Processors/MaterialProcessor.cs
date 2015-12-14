﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using Fusion.Core.Shell;
using Fusion.Engine.Graphics;

namespace Fusion.Build.Processors {

	[AssetProcessor("Materials", "Process material files.")]
	public class MaterialProcessor : AssetProcessor {

		
		/// <summary>
		/// 
		/// </summary>
		public MaterialProcessor ()
		{
		}


		/// <summary>
		/// 
		/// </summary>
		/// <param name="sourceStream"></param>
		/// <param name="targetStream"></param>
		public override void Process ( AssetFile assetFile, BuildContext context )
		{
			var mtrl	=	Material.FromINI ( File.ReadAllText(assetFile.FullSourcePath) );

			var file	=	mtrl.ToINI();

			using ( var target = assetFile.OpenTargetStream() ) {
				using ( var bw = new BinaryWriter(target) ) {
					bw.Write(file);
				}
			}
		}
	}
}