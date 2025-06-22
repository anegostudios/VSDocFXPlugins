using Docfx.DataContracts.Common;
using Docfx.DataContracts.ManagedReference;
using Docfx.Plugins;
using System.Composition;
using System.Security.Cryptography.X509Certificates;
using VintageStoryDocFXPlugin;

namespace VSDocFXJsonOnlyMagicAddon
{
    [Export(typeof(IDocumentProcessor))]
    public class VSJsonMagicAddon : VSProcessorAddon
    {
        public override void OnPageBeingProcessed(PageViewModel page)
        {
            base.OnPageBeingProcessed(page);

            //The current class name is used quite a lot when adding properties, so I can access it here.
            string qualName = page.Items[0].Uid;

            //Time to add new properties!
            //The YML file stores all serialized attributes for each field, so we can use this to find our extra properties.

            //Note that the first item always seems to be the class. Hopefully this remains the case.
            //  If not then this should be changed to a for loop for each item with a minor performance hit.
            if (page.Items[0].Attributes?.Count > 0)
            {

                //We also allow more than one additional property at a time, so this should be a loop.
                foreach (AttributeInfo att in page.Items[0].Attributes)
                {
                    if (att.Type == "Vintagestory.API.AddDocumentationPropertyAttribute")
                    {
                        //Accessing the arguments here. These have to be serializable types by DocFX, otherwise the attribute won't show up.
                        string propName = att.Arguments[0].Value as string;
                        string propWithQual = qualName + "." + propName;

                        //We need to add the default and required tags to the property summary.
                        string propSummary = att.Arguments[1].Value as string;
                        string required = att.Arguments[3].Value as string;
                        string defaultStatus = att.Arguments[4].Value as string;
                        if (defaultStatus == "")
                        {
                            propSummary = "<!--<jsonoptional>" + required + "</jsonoptional>-->\n" + propSummary;
                        }
                        else
                        {
                            propSummary = "<!--<jsonoptional>" + required + "</jsonoptional><jsondefault>" + defaultStatus + "</jsondefault>-->\n" + propSummary;
                        }

                        if (att.Arguments.Count > 5)
                        {
                            bool isAttribute = (bool)att.Arguments[5].Value;
                            if (isAttribute)
                            {
                                propSummary += "[Attribute]";
                            }
                        }

                        string typeWithNamespace = att.Arguments[2].Value as string;
                        int sepIndex = typeWithNamespace.LastIndexOf('.');

                        if (sepIndex == -1)
                        {
                            Console.WriteLine("The additional property of " + propWithQual + "has not given a full namespace. " +
                                "Please append it with the full namespace of the type, not just the type name.");
                        }

                        string typeNS = typeWithNamespace.Substring(0, sepIndex);
                        string typeName = typeWithNamespace.Substring(sepIndex + 1);

                        //Here we add the new property. It needs creating as a new item, but also adding as a child to the class.
                        page.Items[0].Children.Add(propWithQual);
                        page.Items.Add(new ItemViewModel()
                        {
                            Uid = propWithQual,
                            CommentId = "F:" + propWithQual, //'F:' here means 'Field:'
                            Name = propName,
                            Id = propName,
                            Parent = qualName,
                            SupportedLanguages = page.Items[0].SupportedLanguages, //Usually C# and VB (for some reason)
                            NameWithType = page.Items[0].Id + "." + propName,
                            FullName = propWithQual,
                            Type = MemberType.Field,
                            AssemblyNameList = page.Items[0].AssemblyNameList,
                            NamespaceName = page.Items[0].NamespaceName,
                            Summary = propSummary,
                            Syntax = new SyntaxDetailViewModel()
                            {
                                Return = new ApiParameter()
                                {
                                    Type = typeWithNamespace
                                }
                            },
                        });

                        //Search for reference...
                        bool foundReference = false;
                        foreach (var reference in page.References)
                        {
                            if (reference.Uid == typeWithNamespace)
                            {
                                foundReference = true;
                                break;
                            }
                        }

                        //Add reference if not found.
                        if (!foundReference)
                        {
                            bool isSys = typeNS.ToLower().StartsWith("system");
                            string refTypeName = typeWithNamespace;
                            //Array types don't work as expected!
                            //Need to make them reference the original type and just pretend everything is okay.
                            if (refTypeName.Contains("[]", StringComparison.OrdinalIgnoreCase))
                            {
                                refTypeName = refTypeName.Replace("[]", "", StringComparison.OrdinalIgnoreCase);
                            }
                            page.References.Add(new ReferenceViewModel()
                            {
                                Uid = typeWithNamespace,
                                CommentId = "T:" + typeWithNamespace, //'T:' here means 'Type:'
                                Parent = typeNS,
                                IsExternal = isSys,
                                Href = isSys ? "https://learn.microsoft.com/dotnet/api/" + refTypeName : refTypeName + ".html",
                                Name = typeName,
                                NameWithType = typeName,
                                FullName = typeWithNamespace
                            });
                        }

                    }
                }
            }

            //Check page items for document attribute.
            foreach (var item in page.Items)
            {
                if (item.Attributes != null)
                {
                    foreach (AttributeInfo att in item.Attributes)
                    {
                        if (att.Type == "Vintagestory.API.DocumentAsJsonAttribute")
                        {
                            if (att.Arguments.Count > 0)
                            {
                                string required = att.Arguments[0].Value as string;
                                string defaultStatus = att.Arguments[1].Value as string;
                                if (item.Summary == null) item.Summary = "";

                                if (defaultStatus == "")
                                {
                                    item.Summary = "<!--<jsonoptional>" + required + "</jsonoptional>-->\n" + item.Summary;
                                }
                                else
                                {
                                    item.Summary = "<!--<jsonoptional>" + required + "</jsonoptional><jsondefault>" + defaultStatus + "</jsondefault>-->\n" + item.Summary;
                                }

                                if (att.Arguments.Count > 2)
                                {
                                    bool isAttribute = (bool)att.Arguments[2].Value;
                                    if (isAttribute)
                                    {
                                        item.Summary += "[Attribute]";
                                    }
                                }
                            }
                        }
                    }
                }
            }

            //Convert Properties to Fields!
            ConvertYamlPropertiesToRelevantTypes(page);

        }

        public override string GetAddonName()
        {
            return "JSON Edits";
        }

        /// <summary>
        /// Properties (get; set;) and fields are seperated in DocFX, meaning the sorting does not work effectively when using properties.
        /// This function will make DocFX think that all properties are fields, and then turn any attributes into properties, and any enum fields into methods.
        /// These are then handled and renamed by the CSS DocFX template.
        /// </summary>
        protected void ConvertYamlPropertiesToRelevantTypes(PageViewModel page)
        {
            foreach (var item in page.Items)
            {
                if (item.Type == MemberType.Property)
                {
                    item.Type = MemberType.Field;
                    if (item.CommentId != null) item.CommentId = "F" + item.CommentId.Substring(1); //Replaces "P:Comment" with "F:Comment".
                }
                else if (page.Items[0].Type == MemberType.Enum && item.Type == MemberType.Field)
                {
                    item.Type = MemberType.Method;
                    if (item.CommentId != null) item.CommentId = "M" + item.CommentId.Substring(1);
                }

                if (item.Type == MemberType.Field && item.Summary != null && item.Summary.Contains("[Attribute]"))
                {
                    item.Summary = item.Summary.Replace("[Attribute]", "");
                    item.Type = MemberType.Property;
                    if (item.CommentId != null) item.CommentId = "P" + item.CommentId.Substring(1);
                }
            }
        }
    }
}
