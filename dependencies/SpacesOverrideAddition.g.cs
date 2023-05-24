using Elements;
using System.Collections.Generic;
using System;
using System.Linq;

namespace SpacePlanning
{
	/// <summary>
	/// Override metadata for SpacesOverrideAddition
	/// </summary>
	public partial class SpacesOverrideAddition : IOverride
	{
        public static string Name = "Spaces Addition";
        public static string Dependency = null;
        public static string Context = "[*discriminator=Elements.SpaceBoundary]";
		public static string Paradigm = "Edit";

        /// <summary>
        /// Get the override name for this override.
        /// </summary>
        public string GetName() {
			return Name;
		}

		public object GetIdentity() {

			return Identity;
		}

	}

}