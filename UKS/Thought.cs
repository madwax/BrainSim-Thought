/*
 * Brain Simulator Thought
 *
 * Copyright (c) 2026 Charles Simon
 *
 * This file is part of Brain Simulator Thought and is licensed under
 * the MIT License. You may use, copy, modify, merge, publish, distribute,
 * sublicense, and/or sell copies of this software under the terms of
 * the MIT License.
 *
 * See the LICENSE file in the project root for full license information.
 */
using static UKS.UKS;

namespace UKS;

/// <summary>
/// A Thought is an atomic unit of thought. In the lexicon of graphs, a Thought is both a "node" and an Edge.  
/// A Thought can represent anything, physical object, attribute, word, action, feeling, etc.
/// </summary>
/// Thoughs may have labels which are any string...no special characters except '.'. Like comments or variable names, these are typically used for programmer convenience and are not usually 
/// used for functionality but are necessary to save and restore the structure.
/// Labels are case-insensitive although the initial case is preserved within the UKS.
/// Methods which return a Thought may return null in the event no Thought matches the result of the method. Methods which return lists of Thoughts will
/// return a list of zero elements if no Thoughts match the result of the method.
/// A Thought may be referenced by its Label. You can write AddParent("color") [where a Thought is a required parameter.] The system sill automatically retreive a Thought
/// with the given label.
public partial class Thought
{
    public static Thought IsA { get => ThoughtLabels.GetThought("is-a"); }  //this is a cache value shortcut for (Thought)"is-a"
    private readonly List<Thought> _linksTo = new List<Thought>(); //links to "has", "is", is-a, many others
    private readonly List<Thought> _linksFrom = new List<Thought>(); //links from

    /// <summary>
    /// Get an "unsafe" writeable list of a Thought's Links.
    /// This list may change while it is in use and so should not be used as a foreach iterator
    /// </summary>
    public List<Thought> LinksToWriteable { get => _linksTo; }
    /// <summary>
    /// Get an "unsafe" writeable list of Links which target this Thought
    /// </summary>
    public List<Thought> LinksFromWriteable { get => _linksFrom; }
    /// <summary>
    /// Full "Safe" list or links
    /// </summary>
    public IReadOnlyList<Thought> LinksTo { get { lock (_linksTo) { return new List<Thought>(_linksTo.AsReadOnly()); } } }
    /// <summary>
    /// Get a "safe" list of links which target this Thought
    /// </summary>
    public IReadOnlyList<Thought> LinksFrom { get { lock (_linksFrom) { return new List<Thought>(_linksFrom.AsReadOnly()); } } }
    /// <summary>
    /// "Safe" list of direct ancestors atomic thoughts (not links)
    /// </summary>
    public IReadOnlyList<Thought> Parents { get { lock (_linksTo) { return new List<Thought>(_linksTo.Where(x => x.LinkType?.Label == "is-a").Select(x => x.To).ToList().AsReadOnly()); } } }
    /// <summary>
    /// "Safe" list of direct descendants
    /// </summary>
    public IReadOnlyList<Thought> Children { get { lock (_linksFrom) { return new List<Thought>(_linksFrom.Where(x => x.LinkType?.Label == "is-a").Select(x => x.From).ToList().AsReadOnly()); } } }

    private string _label = "";
    /// <summary>
    /// Manages a Thought's label and maintais a hash table
    /// *Restrictions on Thought LabelsNames:
    ///  * must be unique
    ///  * cannot include ' ' (use a - instead)
    ///  * cannot include '.' this is the flag for creating a subclass with following attributes
    ///  * cannot include '*' this is the flag for auto-increment the label
    ///  * case insensitive but initial input case is preserved for display
    ///  * capitalized labels are never signularized even if "singularize=true"
    /// </summary>
    public string Label
    {
        get => _label;
        set
        {
            if (value == _label) return; //label is unchanged
            ThoughtLabels.RemoveThoughtLabel(_label);
            _label = ThoughtLabels.AddThoughtLabel(value, this);
        }
    }

    /// <summary>
    /// Last time this thought was fired; updated by <see cref="Fire"/>.
    /// </summary>
    public DateTime LastFiredTime = DateTime.Now;

    private TimeSpan _timeToLive = TimeSpan.MaxValue;
    /// <summary>
    /// When set, makes a Thought transient.
    /// </summary>
    public TimeSpan TimeToLive
    {
        get { return _timeToLive; }
        set
        {
            _timeToLive = value;
            if (_timeToLive != TimeSpan.MaxValue)
                AddToTransientList();
        }
    }

    //////NEEDED for Link functionality 
    public Thought? _from;
    /// <summary>
    /// the Thought Source
    /// </summary>
    public Thought? From
    {
        get => _from;
        set { _from = value; }
    }
    private Thought? _linkType;
    /// <summary>
    /// The Link Type
    /// </summary>
    public Thought? LinkType
    {
        get { return _linkType; }
        set { _linkType = value; }
    }
    private Thought? _to;
    /// <summary>
    /// Target of the link (if this Thought is a link).
    /// </summary>
    public Thought? To
    {
        get { return _to; }
        set { _to = value; }
    }

    private object _value;
    /// <summary>
    /// Any serializable object can be attached to a Thought.
    /// ONLY STRINGS are supported for save/restore to disk file.
    /// </summary>
    public object V
    {
        get => _value;
        set { this._value = value; }
    }

    private float _weight = 1;
    /// <summary>
    /// Weight of this Thought (link).
    /// </summary>
    public float Weight
    {
        get { return _weight; }
        set
        {
            _weight = value;
            //if this is a commutative link, also set the weight on the reverse
            if (LinkType?.HasProperty("IsCommutative") == true)
            {
                Thought rReverse = To.LinksTo.FindFirst(x => x.LinkType == LinkType && x.To == From);
                if (rReverse is not null)
                {
                    rReverse._weight = _weight;
                }
            }
        }
    }

    //The constructores
    /// <summary>
    /// Default constructor.
    /// </summary>
    public Thought()
    {
    }

    /// <summary>
    /// Copy Constructor.
    /// </summary>
    /// <param name="r">Thought to copy.</param>
    public Thought(Thought r)
    {
        LinkType = r.LinkType;
        From = r.From;
        To = r.To;
        Weight = r.Weight;
        //COPY other properties as needed
    }

    /// <summary>
    /// Returns a Thought's label OR a string represent a link or sequence.  
    /// For a link, the format is "Label[From->LinkType->To]".  
    /// For a sequence, the format is "^elem1elem2elem3".  
    /// If there is a string Value associeted with this Thought, it is added to the end of the line as "_V:value".
    /// </summary>
    /// <returns>Formatted string representation.</returns>
    public override string ToString()
    {
        if (LinkType?.Label == "spelled")
        { }
        string retVal = Label;

        if (From is not null || LinkType is not null || To is not null)
        {
            if (theUKS.IsSequenceElement(this))
            {
                var valuList = theUKS.FlattenSequence(this);
                retVal = "^" + string.Join("", valuList);
                return retVal;
            }

            retVal += "[";
            if (From is not null)
            {
                retVal += From?.ToString();
            }
            if (LinkType is not null)
                retVal += ((retVal == "") ? "" : "->") + LinkType?.ToString();
            if (To is not null)
            {
                retVal += ((retVal == "") ? "" : "->") + To?.ToString();
            }
            retVal += "]";
        }
        if (V is not null)  //if there is a string value, add it to the end of the line
            retVal += "_V:" + V.ToString();

        return retVal;
    }

    /// <summary>
    /// Allows implicit conversion from string to Thought label lookup.
    /// </summary>
    /// <param name="label">Label to resolve.</param>
    /// <returns>The Thought with that label or null.</returns>
    public static implicit operator Thought(string label)
    {
        Thought t = ThoughtLabels.GetThought(label);
        if (t is null)
        { }
        //            throw new ArgumentNullException($"No Thought found with label: {label}");
        return t;
    }

    /// <summary>
    /// Equality by label and links; treats atomic vs link appropriately.
    /// </summary>
    public override bool Equals(object obj)
    {
        if (obj is Thought t)
        {
            if (Label != t.Label) return false;
            //are the links the same?
            if (LinksTo.Count != t.LinksTo.Count) return false;
            for (int i = 0; i < LinksTo.Count; i++)
                if (LinksTo[i] != t.LinksTo[i]) return false;

            if (t.From is null && t.LinkType is null && t.To is null)
            {//must be atomic
                return true;
            }
            if ((t.From is not null || t.LinkType is not null || t.To is not null))
            {
                //this must be a link
                if ((To is null || t.To == To) &&  //
                    (From is null || t.From == From) &&
                    (t.LinkType is not null && t.LinkType == LinkType))
                    return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Reference-based equality helper.
    /// </summary>
    public static bool operator ==(Thought? a, Thought? b)
    {
        //if (a is null && b is null)
        //    return true;
        if (a is null || b is null)
            return false;
        if (a.Label != "" && a.Label == b.Label) return true;
        if (a.To is not null || a.LinkType is not null || a.To is not null)
            if ((a.To is null && b.To is null) || a.To == b.To && a.From == b.From && a.LinkType == b.LinkType)
                return true;
        return false;
    }
    /// <summary>
    /// Reference-based inequality helper.
    /// </summary>
    public static bool operator !=(Thought? a, Thought? b)
    {
        if (a is null && b is null)
            return false;
        if (a is null || b is null)
            return true;
        if (a.Label != b.Label) return true;
        if ((a.To is null || a.To == b.To) &&
            (a.From is null || a.From == b.From) &&
            (a.LinkType is null || a.LinkType == b.LinkType))
            return false;
        return true;
    }

    /// <summary>
    /// Assigns a default label for link Thoughts when missing.
    /// </summary>
    /// <returns>Current thought.</returns>
    public Thought AddDefaultLabel()
    {
        if (this.LinkType is null) return this;
        if (string.IsNullOrEmpty(this.Label))
            Label = "R*";
        return this;
    }

    /// <summary>
    /// Returns direct children plus any subclass children.
    /// </summary>
    public IReadOnlyList<Thought> ChildrenWithSubclasses
    {
        get
        {
            List<Thought> retVal = (List<Thought>)Children;// (List<Thought>)LinksOfType(IsA, true);

            for (int i = 0; i < retVal.Count; i++)
            {
                Thought t = retVal[i];
                if (t.Label.StartsWith(this._label))
                {
                    retVal.AddRange(t.Children);
                    retVal.RemoveAt(i);
                    i--;
                }
            }
            return retVal;
        }
    }

    /// <summary>
    /// Ancestors including self (BFS).
    /// </summary>
    public IEnumerable<Thought> AncestorsWithSelf
    {
        get
        {
            yield return this;
            foreach (var ancestor in Ancestors)
                yield return ancestor;
        }
    }

    /// <summary>
    /// Breadth-first ancestors (excluding self).
    /// </summary>
    public IEnumerable<Thought> Ancestors
    {
        get
        {
            var queue = new Queue<Thought>(Parents);
            var seen = new HashSet<Thought>();

            while (queue.Count > 0)
            {
                var parent = queue.Dequeue();
                if (parent is null || !seen.Add(parent)) continue;

                yield return parent;

                foreach (var gp in parent.Parents)
                    queue.Enqueue(gp);
            }
        }
    }

    /// <summary>
    /// Breadth-first descendants.
    /// </summary>
    public IEnumerable<Thought> Descendants
    {
        get
        {
            var queue = new Queue<Thought>(Children);
            var seen = new HashSet<Thought>();

            while (queue.Count > 0)
            {
                var child = queue.Dequeue();
                if (child is null || !seen.Add(child)) continue;

                yield return child;

                foreach (var gc in child.Children)
                    queue.Enqueue(gc);
            }
        }
    }

    /// <summary>
    /// Determines whether this thought has the specified ancestor (self-inclusive).
    /// </summary>
    /// <param name="t">Ancestor to test.</param>
    /// <returns>True if found; otherwise false.</returns>
    public bool HasAncestor(Thought t)
    {
        foreach (var ancestor in AncestorsWithSelf)
            if (ancestor == t) return true;
        return false;
    }

    /// <summary>
    /// Updates the last-fired time on a Thought.
    /// </summary>
    public void Fire()
    {
        LastFiredTime = DateTime.Now;
        //useCount++;
    }

    //LINKS
    //TODO reverse the parameters so it's type,target
    /// <summary>
    /// Adds a link to a Thought if it does not already exist. The Thought is the source of the link.
    /// </summary>
    /// <param name="target">Target Thought.</param>
    /// <param name="linkType">Relationship type Thought.</param>
    /// <returns>The new or existing link Thought.</returns>
    public Thought AddLink(Thought target, Thought linkType)
    {
        if (linkType is null)  //NULL link types could be allowed in search Thoughtys Parameter?
        {
            return null;
        }

        //does the link already exist?
        Thought r = HasLink(target, linkType);
        if (r is not null)
        {
            //AdjustLink(r.T);
            return r;
        }
        r = new Thought()
        {
            LinkType = linkType,
            From = this,
            To = target,
        };
        if (target is not null && linkType is not null)
        {
            lock (_linksTo)
                lock (target._linksFrom)
                {
                    LinksToWriteable.Add(r);
                    target.LinksFromWriteable.Add(r);
                }
        }
        else
        {
            lock (_linksTo)
            {
                LinksToWriteable.Add(r);
            }
        }
        return r;
    }

    /// <summary>
    /// Removes all links of a given type originating from this thought.
    /// </summary>
    /// <param name="linkType">Link type to remove.</param>
    public void RemoveLinks(Thought linkType)
    {
        for (int i = 0; i < _linksTo.Count; i++)
        {
            Thought r = _linksTo[i];
            if (r.From == this && r.LinkType == linkType)
            {
                RemoveLink(r);
                i--;
            }
        }
    }

    /// <summary>
    /// Finds a link from this source to target with the specified type.
    /// </summary>
    /// <param name="target">Target thought.</param>
    /// <param name="linkType">Link type.</param>
    /// <returns>The matching link or null.</returns>
    private Thought HasLink(Thought target, Thought linkType)
    {
        foreach (Thought r in _linksTo)
        {
            if (r.From == this && r.To == target && r.LinkType == linkType)
                return r;
        }
        return null;
    }

    /// <summary>
    /// Removes a link.
    /// </summary>
    /// <param name="r">The Thought's source neede not be this Thought.</param>
    public void RemoveLink(Thought r)
    {
        if (r is null) return;
        if (r.LinkType is null) return;
        if (r.From is null)
        {
            lock (r.LinkType.LinksFromWriteable)
            {
                lock (r.To.LinksFromWriteable)
                {
                    r.LinkType.LinksFromWriteable.RemoveAll(x => x.From == r.From && x.LinkType == r.LinkType && x.To == r.To);
                    r.To.LinksFromWriteable.RemoveAll(x => x.From == r.From && x.LinkType == r.LinkType && x.To == r.To);
                }
            }
        }
        else if (r.To is null)
        {
            lock (r.From.LinksToWriteable)
            {
                lock (r.LinkType.LinksFromWriteable)
                {
                    r.From.LinksToWriteable.RemoveAll(x => x.From == r.From && x.LinkType == r.LinkType && x.To is null);
                    r.LinkType.LinksFromWriteable.RemoveAll(x => x.From == r.From && x.LinkType == r.LinkType && x.To is null);
                }
            }
        }
        else
        {
            lock (r.From.LinksToWriteable)
            {
                lock (r.LinkType.LinksFromWriteable)
                {
                    lock (r.To.LinksFromWriteable)
                    {
                        r.From.LinksToWriteable.Remove(r);
                        r.LinkType.LinksFromWriteable.Remove(r);
                        r.To.LinksFromWriteable.Remove(r);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Finds a link matching the optional source/type/target criteria.
    /// </summary>
    /// <param name="source">Optional source filter.</param>
    /// <param name="linkType">Optional link type filter.</param>
    /// <param name="targett">Optional target filter.</param>
    /// <returns>Matching link or null.</returns>
    public Thought HasLink(Thought source, Thought linkType, Thought targett)
    {
        if (source is null && linkType is null && targett is null) return null;
        foreach (Thought r in LinksTo)
            if ((source is null || r.From == source) &&
                (linkType is null || r.LinkType == linkType) &&
                (targett is null || r.To == targett)) return r;
        return null;
    }

    /// <summary>
    /// Removes a link of the given type to the given target.
    /// </summary>
    /// <param name="t2">Target thought.</param>
    /// <param name="linkType">Link type.</param>
    /// <returns>The removed link (prototype).</returns>
    public Thought RemoveLink(Thought t2, Thought linkType)
    {
        Thought r = new() { From = this, LinkType = linkType, To = t2 };
        RemoveLink(r);
        return r;
    }

    /// <summary>
    /// Adds a parent link ("is-a") if not already present.
    /// </summary>
    /// <param name="newParent">Parent to add.</param>
    /// <returns>The link thought or existing one.</returns>
    public Thought AddParent(Thought newParent)
    {
        if (newParent is null) return null;
        if (!Parents.Contains(newParent))
        {
            //newParent.AddLink(this, IsA);
            return AddLink(newParent, "is-a");
        }
        return LinksTo.FindFirst(x => x.To == newParent && x.LinkType == IsA);
    }

    /// <summary>
    /// Remove a parent from a Thought.
    /// </summary>
    /// <param name="t">If the Thought is not a parent, the function does nothing.</param>
    public void RemoveParent(Thought t)
    {
        Thought r = new() { From = this, LinkType = IsA, To = t };
        t.RemoveLink(r);
    }

    /// <summary>
    /// Remove a child link ("is-a") from this Thought.
    /// </summary>
    /// <param name="t">Child thought to remove.</param>
    public void RemoveChild(Thought t)
    {
        Thought r = new() { From = t, LinkType = IsA, To = this };
        RemoveLink(r);
    }

    /// <summary>
    /// Gets attributes linked via "hasAttribute" or "is".
    /// </summary>
    /// <returns>List of attribute thoughts.</returns>
    public List<Thought> GetAttributes()
    {
        List<Thought> retVal = new();
        foreach (Thought r in LinksTo)
        {
            if (r.LinkType.Label != "hasAttribute" && r.LinkType.Label != "is") continue;
            retVal.Add(r.To);
        }
        return retVal;
    }

    /// <summary>
    /// Determines whether this thought has the specified property, considering inheritance.
    /// </summary>
    /// <param name="t">Property thought to test.</param>
    /// <returns>True if the property is present; otherwise false.</returns>
    public bool HasProperty(Thought t)  //with inheritance
    {
        //NOT thread safe
        if (t is null) return false;
        if (LinksTo.FindFirst(x => x.LinkType.Label == "hasProperty" && x.To == t) is not null) return true;

        foreach (Thought t1 in Ancestors) //handle inheritance 
        {
            if (t1.LinksTo.FindFirst(x => x.LinkType.Label == "hasProperty" && x.To == t) is not null) return true;
        }
        return false;
    }

    private void AddToTransientList()
    {
        if (!UKS.transientLinks.Contains(this))
            UKS.transientLinks.Add(this);
    }

    /// <summary>
    /// Enumerate the closure starting from this Thought (root) using a queue (BFS):
    /// - yields root
    /// - follows all outgoing LinksTo (includes the link-thought, its LinkType, its To)
    /// - also follows incoming "is-a" LinksFrom (includes the link-thought, its LinkType, its From)
    /// Cycle-safe via reference-identity visited set (not labels).
    /// </summary>
    /// <returns>Enumerable of sub-thoughts.</returns>
    public IEnumerable<Thought> EnumerateSubThoughts()
    {
        var visited = new HashSet<Thought>();
        var q = new Queue<Thought>();

        void EnqueueIfNew(Thought? t)
        {
            if (t is null) return;
            if (visited.Add(t))
                q.Enqueue(t);
        }

        // start
        EnqueueIfNew(this);
        foreach (var isaLink in this.LinksTo.Where(x => x.LinkType?.Label == "is-a"))
        {
            yield return isaLink;
        }

        while (q.Count > 0)
        {
            var t = q.Dequeue();
            if (t is null) continue;
            if (t.From is not null || t.To is not null || t.LinkType is not null)
                yield return t;
            else
            { }

            EnqueueIfNew(t.LinkType);
            EnqueueIfNew(t.To);
            foreach (var isaLink in t.LinksFrom.Where(x => x.LinkType?.Label == "is-a"))  //get all the children of this Thought
            {
                EnqueueIfNew(isaLink);
                EnqueueIfNew(isaLink.From);
            }
            foreach (var link in t.LinksTo.Where(x => x.LinkType?.Label != "is-a")) //don't get the parents again
            {
                EnqueueIfNew(link);
                EnqueueIfNew(link.To);
            }

            //is this a seq?
            EnqueueIfNew(t.LinkType);
            EnqueueIfNew(t.To);
        }
    }
}
