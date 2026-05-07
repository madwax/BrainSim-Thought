using System;
using System.Collections.Generic;
using UKS;

namespace BrainSimulator.Modules
{
    public class LinkDest
    {
        public Thought linkType;
        public Thought target;
        public List<Link> links = new();
        public LinkDest()
        { }
        public LinkDest( Link r )
        {
            linkType = r.LinkType;
            target = r.To;
            links.Add( r );
        }
        public override string ToString()
        {
            return $"{linkType.Label} -> {target.Label}  :  {links.Count}";
        }
    }
}
