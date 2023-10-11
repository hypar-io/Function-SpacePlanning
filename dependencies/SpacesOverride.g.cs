using Elements;
using System.Collections.Generic;
using System;
using System.Linq;

namespace SpacePlanning
{
    /// <summary>
    /// Override metadata for SpacesOverride
    /// </summary>
    public partial class SpacesOverride : IOverride
    {
        public static string Name = "Spaces";
        public static string Dependency = null;
        public static string Context = "[*discriminator=Elements.SpaceBoundary]";
        public static string Paradigm = "Edit";

        /// <summary>
        /// Get the override name for this override.
        /// </summary>
        public string GetName()
        {
            return Name;
        }

        public object GetIdentity()
        {

            return Identity;
        }

    }
    public static class SpacesOverrideExtensions
    {
        /// <summary>
        /// Apply Spaces edit overrides to a collection of existing elements
        /// </summary>
        /// <param name="overrideData">The Spaces Overrides to apply</param>
        /// <param name="existingElements">A collection of existing elements to which to apply the overrides.</param>
        /// <param name="identityMatch">A function returning a boolean which indicates whether an element is a match for an override's identity.</param>
        /// <param name="modifyElement">A function to modify a matched element, returning the modified element.</param>
        /// <typeparam name="T">The element type this override applies to. Should match the type(s) in the override's context.</typeparam>
        /// <returns>A collection of elements, including unmodified and modified elements from the supplied collection.</returns>
        public static List<T> Apply<T>(
            this IList<SpacesOverride> overrideData,
            IEnumerable<T> existingElements,
            Func<T, SpacesIdentity, bool> identityMatch,
            Func<T, SpacesOverride, T> modifyElement) where T : Element
        {
            var resultElements = new List<T>(existingElements);
            if (overrideData != null)
            {
                foreach (var overrideValue in overrideData)
                {
                    // Assuming there will only be one match per identity, find the first element that matches.
                    var matchingElement = existingElements.FirstOrDefault(e => identityMatch(e, overrideValue.Identity));
                    // if we found a match,
                    if (matchingElement != null)
                    {
                        // remove the old matching element
                        resultElements.Remove(matchingElement);
                        // apply the modification function to it
                        var modifiedElement = modifyElement(matchingElement, overrideValue);
                        // set the identity
                        Identity.AddOverrideIdentity(modifiedElement, overrideValue);
                        //and re-add it to the collection
                        resultElements.Add(modifiedElement);
                    }
                }
            }
            return resultElements;
        }

        /// <summary>
        /// Apply Spaces edit overrides to a collection of existing elements
        /// </summary>
        /// <param name="existingElements">A collection of existing elements to which to apply the overrides.</param>
        /// <param name="overrideData">The Spaces Overrides to apply — typically `input.Overrides.Spaces`</param>
        /// <param name="identityMatch">A function returning a boolean which indicates whether an element is a match for an override's identity.</param>
        /// <param name="modifyElement">A function to modify a matched element, returning the modified element.</param>
        /// <typeparam name="T">The element type this override applies to. Should match the type(s) in the override's context.</typeparam>
        /// <returns>A collection of elements, including unmodified and modified elements from the supplied collection.</returns>
        public static void ApplyOverrides<T>(
            this List<T> existingElements,
            IList<SpacesOverride> overrideData,
            Func<T, SpacesIdentity, bool> identityMatch,
            Func<T, SpacesOverride, T> modifyElement
            ) where T : Element
        {
            var updatedElements = overrideData.Apply(existingElements, identityMatch, modifyElement);
            existingElements.Clear();
            existingElements.AddRange(updatedElements);
        }

        /// <summary>
        /// Create elements from add/removeoverrides, and apply any edits.
        /// </summary>
        /// <param name="edits">The collection of edit overrides (Overrides.Spaces)</param>
        /// <param name="additions">The collection of add overrides (Overrides.Additions.Spaces)</param>
        /// <param name="removals">The collection of remove overrides (Overrides.Removals.Spaces)</param>        /// <param name="identityMatch">A function returning a boolean which indicates whether an element is a match for an override's identity.</param>
        /// <param name="createElement">A function to create a new element, returning the created element.</param>
        /// <param name="modifyElement">A function to modify a matched element, returning the modified element.</param>
        /// <param name="existingElements">An optional collection of existing elements to which to apply any edit overrides, or remove if remove overrides are found.</param>
        /// <typeparam name="T">The element type this override applies to. Should match the type(s) in the override's context.</typeparam>
        /// <returns>A collection of elements, including new, unmodified, and modified elements from the supplied collection.</returns>
        public static List<T> CreateElements<T>(
            this IList<SpacesOverride> edits,
            IList<SpacesOverrideAddition> additions,
            IList<SpacesOverrideRemoval> removals, Func<SpacesOverrideAddition, T> createElement,
            Func<T, SpacesIdentity, bool> identityMatch,
            Func<T, SpacesOverride, T> modifyElement,
            IEnumerable<T> existingElements = null
            ) where T : Element
        {
            List<T> resultElements = existingElements == null ? new List<T>() : new List<T>(existingElements);
            if (removals != null)
            {
                foreach (var removedElement in removals)
                {
                    var elementToRemove = resultElements.FirstOrDefault(e => identityMatch(e, removedElement.Identity));
                    if (elementToRemove != null)
                    {
                        resultElements.Remove(elementToRemove);
                    }
                }
            }
            if (additions != null)
            {
                foreach (var addedElement in additions)
                {
                    var elementToAdd = createElement(addedElement);
                    resultElements.Add(elementToAdd);
                    Identity.AddOverrideIdentity(elementToAdd, addedElement);
                }
            }
            if (edits != null)
            {
                foreach (var editedElement in edits)
                {
                    if (editedElement.Value.ProgramType == "cafe")
                    {
                        double tt = 0;
                    }

                    var elementToEdit = resultElements.FirstOrDefault(e => identityMatch(e, editedElement.Identity));
                    if (elementToEdit != null)
                    {
                        resultElements.Remove(elementToEdit);
                        var newElement = modifyElement(elementToEdit, editedElement);
                        resultElements.Add(newElement);
                        Identity.AddOverrideIdentity(newElement, editedElement);
                    }
                }
            }
            return resultElements;
        }

    }


}