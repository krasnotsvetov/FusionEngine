﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Fusion.Engine.Graphics.GIS.GlobeMath;

namespace Fusion.Engine.Graphics.GIS
{
	public static class GeoHelper
	{
		public static readonly double EarthRadius = 6378.137;

		public static bool LineIntersection(DVector3 lineOrigin, DVector3 lineEnd, double radius, out DVector3[] intersectionPoints)
		{
			intersectionPoints = new DVector3[0];

			double sphereRadius = radius;
			DVector3 sphereCenter = DVector3.Zero;

			var lineDir = new DVector3(lineEnd.X - lineOrigin.X, lineEnd.Y - lineOrigin.Y, lineEnd.Z - lineOrigin.Z);
			lineDir.Normalize();

			DVector3 w = lineOrigin - sphereCenter;

			double a = DVector3.Dot(lineDir, lineDir); // 1.0f;
			double b = 2 * DVector3.Dot(lineDir, w);
			double c = DVector3.Dot(w, w) - sphereRadius * sphereRadius;
			double d = b * b - 4.0f * a * c;

			if (d < 0) return false;

			if (d == 0.0) {
				double x1 = (-b - Math.Sqrt(d)) / (2.0 * a);

				intersectionPoints = new DVector3[1];
				intersectionPoints[0] = lineOrigin + lineDir * x1;

				return true;
			}

			if (d > 0.0f) {
				double sqrt = Math.Sqrt(d);

				double x1 = (-b - sqrt) / (2.0 * a);
				double x2 = (-b + sqrt) / (2.0 * a);

				intersectionPoints = new DVector3[2];
				intersectionPoints[0] = lineOrigin + lineDir * x1;
				intersectionPoints[1] = lineOrigin + lineDir * x2;

				return true;
			}

			return false;
		}


		public static void CartesianToSpherical(DVector3 cart, out double lon, out double lat)
		{
			var radius = cart.Length();

			if (radius == 0.0) {
				lon = 0;
				lat = 0;
				return;
			}

			lon = Math.Atan2(cart.X, cart.Z);
			lat = Math.Asin(cart.Y / radius);
		}


		public static DVector2 CartesianToSpherical(DVector3 cart)
		{
			double lon, lat;
			CartesianToSpherical(cart, out lon, out lat);
			return new DVector2(lon, lat);
		}


		/// <summary>
		/// 
		/// </summary>
		/// <param name="lon"></param>
		/// <param name="lat"></param>
		/// <param name="radius"></param>
		/// <param name="cart"></param>
		public static void SphericalToCartesian(double lon, double lat, float radius, out DVector3 cart)
		{
			cart = DVector3.Zero;

			double x, y, z;
			SphericalToCartesian(lon, lat, radius, out x, out y, out z);

			cart.X = x;
			cart.Y = y;
			cart.Z = z;
		}



		public static void SphericalToCartesian(double lon, double lat, double radius, out double x, out double y, out double z)
		{
			z = (radius * Math.Cos(lat) * Math.Cos(lon));
			x = (radius * Math.Cos(lat) * Math.Sin(lon));
			y = (radius * Math.Sin(lat));
		}


		public static DVector3 SphericalToCartesian(DVector2 lonLat, double radius)
		{
			double x, y, z;
			SphericalToCartesian(lonLat.X, lonLat.Y, radius, out x, out y, out z);
			return new DVector3(x, y, z);
		}


		public static double DistanceBetweenTwoPoints(DVector2 lonLatP0, DVector2 lonLatP1, double earthRadius = 6378.137)
		{
			var phi0 = lonLatP0.Y;
			var phi1 = lonLatP1.Y;
			var deltaPhi = phi1 - phi0;
			var deltaLam = lonLatP1.X - lonLatP0.X;

			var a = Math.Sin(deltaPhi / 2) * Math.Sin(deltaPhi / 2) +
					Math.Cos(phi0) * Math.Cos(phi1) *
					Math.Sin(deltaLam / 2) * Math.Sin(deltaLam / 2);
			var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));

			var d = earthRadius * c;

			return d;
		}


		public static DVector2 RhumbDestinationPoint(DVector2 startPoint, double bearing, double distance, double radius = 6378.137)
		{
			var δ	= distance / radius; // angular distance in radians
			var φ1	= DMathUtil.DegreesToRadians(startPoint.Y);
			var λ1	= DMathUtil.DegreesToRadians(startPoint.X);
			var θ	= DMathUtil.DegreesToRadians(bearing);

			var Δφ = δ * Math.Cos(θ);
			var φ2 = φ1 + Δφ;

			// check for some daft bugger going past the pole, normalise latitude if so
			if (Math.Abs(φ2) > Math.PI / 2) φ2 = φ2 > 0 ? Math.PI - φ2 : -Math.PI - φ2;

			var Δψ = Math.Log(Math.Tan(φ2 / 2 + Math.PI / 4) / Math.Tan(φ1 / 2 + Math.PI / 4));
			var q = Math.Abs(Δψ) > 10e-12 ? Δφ / Δψ : Math.Cos(φ1); // E-W course becomes ill-conditioned with 0/0

			var Δλ = δ * Math.Sin(θ) / q;
			var λ2 = λ1 + Δλ;



			return new DVector2((DMathUtil.RadiansToDegrees(λ2) + 540) % 360 - 180, DMathUtil.RadiansToDegrees(φ2)); // normalise to −180…+180°
		}
	}
}
