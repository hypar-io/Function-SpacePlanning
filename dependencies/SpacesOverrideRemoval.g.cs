using Elements;
using System.Collections.Generic;
using System;
using System.Linq;

namespace SpacePlanning
{
	/// <summary>
	/// Override metadata for SpacesOverrideRemoval
	/// </summary>
	public partial class SpacesOverrideRemoval : IOverride
	{
        public static string Name = "Spaces Removal";
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