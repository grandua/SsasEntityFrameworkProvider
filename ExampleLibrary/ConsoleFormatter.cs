using System;
using AgileDesign.Utilities;

namespace UsageExample
{
    public class ConsoleFormatter
    {
        public void WriteColumnValue(object value, int displayLength)
        {
            if(value == null)
            {
                Console.Write("".PadRight(displayLength));
                return;
            }
            string formattedValue = value.ToString();
            if(value is DateTime)
            {
                formattedValue = ( (DateTime)value ).ToShortDateString();
            }
            Console.Write
                (
                    (string)AlignedValue
                                (
                                    value
                                    , formattedValue.Left(displayLength)
                                    , displayLength
                                )
                );
            
            AddSpaceBetweenColumns();
        }

        void AddSpaceBetweenColumns()
        {
            Console.Write(new string(' ', 2));
        }

        string AlignedValue
            (
            object value
            , string formattedValue
            , int displayLength
            )
        {
            if(value is string)
            {
                return formattedValue.PadRight(displayLength);
            }
            return formattedValue.PadLeft(displayLength);
        }
    }
}