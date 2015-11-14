using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Altus.Suffūz.Serialization
{
    public class ContentTypesSection : ConfigurationSection
    {
        [ConfigurationProperty("contentTypes", IsDefaultCollection = false)]
        [ConfigurationCollection(typeof(ContentTypeElement), AddItemName = "add")]
        public ContentTypeCollection ContentTypes
        {
            get
            {
                return (ContentTypeCollection)this["contentTypes"];
            }
            set
            {
                this["contentTypes"] = value;
            }
        }
    }

    public class ContentTypeCollection : ConfigurationElementCollection
    {
        protected override ConfigurationElement CreateNewElement()
        {
            return new ContentTypeElement();
        }

        protected override object GetElementKey(ConfigurationElement element)
        {
            ContentTypeElement service = (ContentTypeElement)element;

            return getKey(service);
        }

        /// <summary>
        /// Gets or sets the named service element for the given index.
        /// </summary>
        /// <param name="index">The index of the named service element to get or set.</param>
        /// <returns>The named service element.</returns>
        public ContentTypeElement this[int index]
        {
            get
            {
                return (ContentTypeElement)BaseGet(index);
            }
            set
            {
                if (BaseGet(index) != null)
                {
                    BaseRemove(index);
                }
                BaseAdd(index, value);
            }
        }

        /// <summary>
        /// Gets or sets the named service element for the given name.
        /// </summary>
        /// <param name="name">The name of the named service element to get or set.</param>
        /// <returns>The named service element.</returns>
        public new ContentTypeElement this[string name]
        {
            get
            {
                return (ContentTypeElement)BaseGet(name);
            }
        }

        /// <summary>
        /// Gets the number of named service elements in this instance.
        /// </summary>
        public new int Count
        {
            get { return base.Count; }
        }

        public int IndexOf(ContentTypeElement service)
        {
            return BaseIndexOf(service);
        }

        public void RemoveAt(int index)
        {
            BaseRemoveAt(index);
        }

        public void Add(ContentTypeElement item)
        {
            BaseAdd(item);
        }

        public void Clear()
        {
            BaseClear();
        }

        public bool Contains(ContentTypeElement item)
        {
            return BaseIndexOf(item) >= 0;
        }

        public void CopyTo(ContentTypeElement[] array, int arrayIndex)
        {
            base.CopyTo(array, arrayIndex);
        }

        public new bool IsReadOnly
        {
            get { return false; }
        }

        public bool Remove(ContentTypeElement item)
        {
            if (BaseIndexOf(item) >= 0)
            {
                BaseRemove(item);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Gets the key by which named service elements are mapped in the base class.
        /// </summary>
        /// <param name="service">The named service element to get the key from.</param>
        /// <returns>The key.</returns>
        private string getKey(ContentTypeElement service)
        {
            return service.Extension;
        }
    }

    public class ContentTypeElement : ConfigurationElement
    {
        [ConfigurationProperty("extension", IsRequired = true)]
        public string Extension
        {
            get
            {
                return (string)this["extension"];
            }
            set
            {
                this["extension"] = value;
            }
        }

        [ConfigurationProperty("contentType", IsRequired = true)]
        public string ContentType
        {
            get
            {
                return (string)this["contentType"];
            }
            set
            {
                this["contentType"] = value;
            }
        }
    }
}
