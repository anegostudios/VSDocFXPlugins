using Docfx.Build.ManagedReference;
using Docfx.DataContracts.ManagedReference;
using Docfx.Plugins;
using System;
using System.Collections.Generic;
using System.Composition;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VintageStoryDocFXPlugin
{
    /// <summary>
    /// Used as an abstract interface that allows us to hook onto the page being processed.
    /// All addons must also have the below export attribute.
    /// </summary>
    [Export(typeof(IDocumentProcessor))]
    public abstract class VSProcessorAddon : ManagedReferenceDocumentProcessor
    {
        public VSProcessorAddon() 
        {
            //Register the processor addon for use.
            VSDocumentProcessor.RegisterProcessorAddon(this);
            Console.WriteLine("Registered Addon: " + GetAddonName());
        }

        /// <summary>
        /// Since this is quite hacky, we want to have this to ensure that this class isn't actually used as a document processor.
        /// </summary>
        public override ProcessingPriority GetProcessingPriority(FileAndType file)
        {
            return ProcessingPriority.NotSupported;
        }

        /// <summary>
        /// Called by <see cref="VSDocumentProcessor"/> when a page is being processed.
        /// </summary>
        public virtual void OnPageBeingProcessed(PageViewModel page)
        {}

        public abstract string GetAddonName();

    }
}
