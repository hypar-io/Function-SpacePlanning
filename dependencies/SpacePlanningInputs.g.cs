// This code was generated by Hypar.
// Edits to this code will be overwritten the next time you run 'hypar init'.
// DO NOT EDIT THIS FILE.

using Elements;
using Elements.GeoJSON;
using Elements.Geometry;
using Elements.Geometry.Solids;
using Elements.Validators;
using Elements.Serialization.JSON;
using Hypar.Functions;
using Hypar.Functions.Execution;
using Hypar.Functions.Execution.AWS;
using Hypar.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using Line = Elements.Geometry.Line;
using Polygon = Elements.Geometry.Polygon;

namespace SpacePlanning
{
    #pragma warning disable // Disable all warnings

    [System.CodeDom.Compiler.GeneratedCode("NJsonSchema", "10.1.21.0 (Newtonsoft.Json v12.0.0.0)")]
    
    public  class SpacePlanningInputs : S3Args
    
    {
        [Newtonsoft.Json.JsonConstructor]
        
        public SpacePlanningInputs(InputData @oldSpaceBoundaries, Overrides @overrides, string bucketName, string uploadsBucket, Dictionary<string, string> modelInputKeys, string gltfKey, string elementsKey, string ifcKey):
        base(bucketName, uploadsBucket, modelInputKeys, gltfKey, elementsKey, ifcKey)
        {
            var validator = Validator.Instance.GetFirstValidatorForType<SpacePlanningInputs>();
            if(validator != null)
            {
                validator.PreConstruct(new object[]{ @oldSpaceBoundaries, @overrides});
            }
        
            this.OldSpaceBoundaries = @oldSpaceBoundaries;
            this.Overrides = @overrides ?? this.Overrides;
        
            if(validator != null)
            {
                validator.PostConstruct(this);
            }
        }
    
        /// <summary>This input is just for migration purposes. If you have an exported model containing space boundaries, you can import it here to pre-load this function with your previous results and continue editing.</summary>
        [Newtonsoft.Json.JsonProperty("Old Space Boundaries", Required = Newtonsoft.Json.Required.Default, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public InputData OldSpaceBoundaries { get; set; }
    
        [Newtonsoft.Json.JsonProperty("overrides", Required = Newtonsoft.Json.Required.Default, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public Overrides Overrides { get; set; } = new Overrides();
    
    }
    
    [System.CodeDom.Compiler.GeneratedCode("NJsonSchema", "10.1.21.0 (Newtonsoft.Json v12.0.0.0)")]
    
    public partial class Overrides 
    
    {
        public Overrides() { }
        
        [Newtonsoft.Json.JsonConstructor]
        public Overrides(IList<LevelLayoutOverride> @levelLayout, IList<ProgramAssignmentOverride> @programAssignment)
        {
            var validator = Validator.Instance.GetFirstValidatorForType<Overrides>();
            if(validator != null)
            {
                validator.PreConstruct(new object[]{ @levelLayout, @programAssignment});
            }
        
            this.LevelLayout = @levelLayout ?? this.LevelLayout;
            this.ProgramAssignment = @programAssignment ?? this.ProgramAssignment;
        
            if(validator != null)
            {
                validator.PostConstruct(this);
            }
        }
    
        [Newtonsoft.Json.JsonProperty("Level Layout", Required = Newtonsoft.Json.Required.Default, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public IList<LevelLayoutOverride> LevelLayout { get; set; } = new List<LevelLayoutOverride>();
    
        [Newtonsoft.Json.JsonProperty("Program Assignment", Required = Newtonsoft.Json.Required.Default, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public IList<ProgramAssignmentOverride> ProgramAssignment { get; set; } = new List<ProgramAssignmentOverride>();
    
    }
    
    [System.CodeDom.Compiler.GeneratedCode("NJsonSchema", "10.1.21.0 (Newtonsoft.Json v12.0.0.0)")]
    
    public partial class LevelLayoutOverride 
    
    {
        [Newtonsoft.Json.JsonConstructor]
        public LevelLayoutOverride(string @id, LevelLayoutIdentity @identity, LevelLayoutValue @value)
        {
            var validator = Validator.Instance.GetFirstValidatorForType<LevelLayoutOverride>();
            if(validator != null)
            {
                validator.PreConstruct(new object[]{ @id, @identity, @value});
            }
        
            this.Id = @id;
            this.Identity = @identity;
            this.Value = @value;
        
            if(validator != null)
            {
                validator.PostConstruct(this);
            }
        }
    
        [Newtonsoft.Json.JsonProperty("id", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public string Id { get; set; }
    
        [Newtonsoft.Json.JsonProperty("Identity", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public LevelLayoutIdentity Identity { get; set; }
    
        [Newtonsoft.Json.JsonProperty("Value", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public LevelLayoutValue Value { get; set; }
    
    }
    
    [System.CodeDom.Compiler.GeneratedCode("NJsonSchema", "10.1.21.0 (Newtonsoft.Json v12.0.0.0)")]
    
    public partial class ProgramAssignmentOverride 
    
    {
        [Newtonsoft.Json.JsonConstructor]
        public ProgramAssignmentOverride(string @id, ProgramAssignmentIdentity @identity, ProgramAssignmentValue @value)
        {
            var validator = Validator.Instance.GetFirstValidatorForType<ProgramAssignmentOverride>();
            if(validator != null)
            {
                validator.PreConstruct(new object[]{ @id, @identity, @value});
            }
        
            this.Id = @id;
            this.Identity = @identity;
            this.Value = @value;
        
            if(validator != null)
            {
                validator.PostConstruct(this);
            }
        }
    
        [Newtonsoft.Json.JsonProperty("id", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public string Id { get; set; }
    
        [Newtonsoft.Json.JsonProperty("Identity", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public ProgramAssignmentIdentity Identity { get; set; }
    
        [Newtonsoft.Json.JsonProperty("Value", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public ProgramAssignmentValue Value { get; set; }
    
    }
    
    [System.CodeDom.Compiler.GeneratedCode("NJsonSchema", "10.1.21.0 (Newtonsoft.Json v12.0.0.0)")]
    
    public partial class LevelLayoutIdentity 
    
    {
        [Newtonsoft.Json.JsonConstructor]
        public LevelLayoutIdentity(string @addId)
        {
            var validator = Validator.Instance.GetFirstValidatorForType<LevelLayoutIdentity>();
            if(validator != null)
            {
                validator.PreConstruct(new object[]{ @addId});
            }
        
            this.AddId = @addId;
        
            if(validator != null)
            {
                validator.PostConstruct(this);
            }
        }
    
        [Newtonsoft.Json.JsonProperty("Add Id", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public string AddId { get; set; }
    
    }
    
    [System.CodeDom.Compiler.GeneratedCode("NJsonSchema", "10.1.21.0 (Newtonsoft.Json v12.0.0.0)")]
    
    public partial class LevelLayoutValue 
    
    {
        [Newtonsoft.Json.JsonConstructor]
        public LevelLayoutValue(IList<Profile> @profiles)
        {
            var validator = Validator.Instance.GetFirstValidatorForType<LevelLayoutValue>();
            if(validator != null)
            {
                validator.PreConstruct(new object[]{ @profiles});
            }
        
            this.Profiles = @profiles;
        
            if(validator != null)
            {
                validator.PostConstruct(this);
            }
        }
    
        [Newtonsoft.Json.JsonProperty("Profiles", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public IList<Profile> Profiles { get; set; }
    
    }
    
    [System.CodeDom.Compiler.GeneratedCode("NJsonSchema", "10.1.21.0 (Newtonsoft.Json v12.0.0.0)")]
    
    public partial class ProgramAssignmentIdentity 
    
    {
        [Newtonsoft.Json.JsonConstructor]
        public ProgramAssignmentIdentity(string @levelAddId, Vector3 @relativePosition)
        {
            var validator = Validator.Instance.GetFirstValidatorForType<ProgramAssignmentIdentity>();
            if(validator != null)
            {
                validator.PreConstruct(new object[]{ @levelAddId, @relativePosition});
            }
        
            this.LevelAddId = @levelAddId;
            this.RelativePosition = @relativePosition;
        
            if(validator != null)
            {
                validator.PostConstruct(this);
            }
        }
    
        [Newtonsoft.Json.JsonProperty("Level Add Id", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public string LevelAddId { get; set; }
    
        [Newtonsoft.Json.JsonProperty("Relative Position", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public Vector3 RelativePosition { get; set; }
    
    }
    
    [System.CodeDom.Compiler.GeneratedCode("NJsonSchema", "10.1.21.0 (Newtonsoft.Json v12.0.0.0)")]
    
    public partial class ProgramAssignmentValue 
    
    {
        [Newtonsoft.Json.JsonConstructor]
        public ProgramAssignmentValue(string @programType, double? @height)
        {
            var validator = Validator.Instance.GetFirstValidatorForType<ProgramAssignmentValue>();
            if(validator != null)
            {
                validator.PreConstruct(new object[]{ @programType, @height});
            }
        
            this.ProgramType = @programType;
            this.Height = @height;
        
            if(validator != null)
            {
                validator.PostConstruct(this);
            }
        }
    
        /// <summary>What program should be assigned to this zone?</summary>
        [Newtonsoft.Json.JsonProperty("Program Type", Required = Newtonsoft.Json.Required.Default, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public string ProgramType { get; set; }
    
        /// <summary>The height of this space.</summary>
        [Newtonsoft.Json.JsonProperty("Height", Required = Newtonsoft.Json.Required.Default, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        [System.ComponentModel.DataAnnotations.Range(0.01D, double.MaxValue)]
        public double? Height { get; set; }
    
    }
}