//---------------------------------------------------------------------
// <copyright file="SqlWriter.cs" company="Microsoft">
//      Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//---------------------------------------------------------------------

using System.IO;
using System.Text;

namespace SqlEntityFrameworkProvider
{
    /// <summary>
    /// This extends StringWriter primarily to add the ability to add an indent
    /// to each line that is written out.
    /// </summary>
    internal class SqlWriter : StringWriter
    {
        // We start at -1, since the first select statement will increment it to 0.
        int indent = -1;
        /// <summary>
        /// The number of tabs to be added at the beginning of each new line.
        /// </summary>
        internal int Indent
        {
            get { return indent; }
            set { indent = value; }
        }

        bool atBeginningOfLine = true;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="b"></param>
        public SqlWriter(StringBuilder b)
            : base(b, System.Globalization.CultureInfo.InvariantCulture)
        {
        }

        /// <summary>
        /// Reset atBeginningofLine if we detect the newline string.
        /// <see cref="SqlBuilder.AppendLine"/>
        /// Add as many tabs as the value of indent if we are at the 
        /// beginning of a line.
        /// </summary>
        /// <param name="value"></param>
        public override void Write(string value)
        {
            if (value == "\r\n")
            {
                base.WriteLine();
                atBeginningOfLine = true;
            }
            else
            {
                if (atBeginningOfLine)
                {
                    if (indent > 0)
                    {
                        base.Write(new string('\t', indent));
                    }
                    atBeginningOfLine = false;
                }
                base.Write(value);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        public override void WriteLine()
        {
            base.WriteLine();
            atBeginningOfLine = true;
        }
    }
}
