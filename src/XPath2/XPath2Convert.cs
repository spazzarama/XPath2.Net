﻿using System;
using System.Globalization;
using System.Text;
using System.Xml;
using System.Xml.Schema;
using System.Xml.XPath;
using Wmhelp.XPath2.Properties;
using Wmhelp.XPath2.Value;
#if !(NET35)
using System.Numerics;
#endif

namespace Wmhelp.XPath2
{
    public static class XPath2Convert
    {
        public static string ToString(object value)
        {
            if (value == null)
                return "false";
            if (value is Decimal)
                return ToString((decimal)value);
            else if (value is Double)
                return ToString((double)value);
            else if (value is Single)
                return ToString((float)value);
            else if (value is Boolean)
                return ToString((bool)value);
            else if (value is DateTime)
                return new DateTimeValue(false, (DateTime)value).ToString();
            else if (value is TimeSpan)
                return new DayTimeDurationValue((TimeSpan)value).ToString();
            return value.ToString();
        }

        public static string ToString(bool value)
        {
            return value ? "true" : "false";
        }

        public static string ToString(decimal value)
        {
            if (value != Decimal.Truncate(value))
                return value.ToString("0.0#################", CultureInfo.InvariantCulture);
            return value.ToString("0", CultureInfo.InvariantCulture);
        }

#if NET35
        public static string ToString(double value)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }

        public static string ToString(float value)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }
#else
        // Based on a published algorithm by Guy L. Steele and Jon L. White.
        // Contributor(s): the fppfppExponential routine, and some of the constant declarations 
        // are from the class FloatingPointConverter by Michael H. Kay

        private static readonly char[] charForDigit =
        {
            '0', '1', '2', '3', '4', '5', '6', '7', '8', '9'
        };

        private static readonly int floatExpMask = 0x7f800000;
        private static readonly int floatExpShift = 23;
        private static readonly int floatExpBias = 127;
        private static readonly int floatFractMask = 0x7fffff;

        private static readonly long doubleFractMask = 0xfffffffffffffL;
        private static readonly long doubleExpMask = 0x7ff0000000000000L;
        private static readonly int doubleExpShift = 52;
        private static readonly int doubleExpBias = 1023;

        public static string ToString(double value)
        {
            //if ((value >= 1E-06 && value < 1000000.0) || 
            //    (value > -1000000.0 && value <= -1E-06))
            //    return value.ToString(CultureInfo.InvariantCulture);
            if (Double.IsPositiveInfinity(value))
                return "INF";
            else if (Double.IsNegativeInfinity(value))
                return "-INF";
            else if (Double.IsNaN(value))
                return "NaN";
            if (value == 0.0)
            {
                if (double.IsPositiveInfinity(1.0 / value))
                    return "0";
                if (double.IsNegativeInfinity(1.0 / value))
                    return "-0";
            }
            StringBuilder sb = new StringBuilder();
            if (value < 0)
            {
                sb.Append('-');
                value = -value;
            }
            long bits = BitConverter.DoubleToInt64Bits(value);
            long fraction = (1L << 52) | (bits & doubleFractMask);
            long rawExp = (bits & doubleExpMask) >> doubleExpShift;
            int exp = (int)rawExp - doubleExpBias;
            if (value < 1E-06F || value >= 1000000F)
                fppfppExponential(sb, exp, fraction, 52);
            else if (value <= 0.01)
                fppfppBig(sb, exp, fraction, 52);
            else
                fppfpp(sb, exp, fraction, 52);
            return sb.ToString();
        }

        public static string ToString(float value)
        {
            //if ((value >= 1E-06f && value < 1000000f) || (value > -1000000f && value <= -1E-06f))
            //    return value.ToString(CultureInfo.InvariantCulture);
            if (value == 0f)
            {
                if (Single.IsPositiveInfinity(1f / value))
                    return "0";
                if (Single.IsNegativeInfinity(1f / value))
                    return "-0";
            }
            if (Single.IsPositiveInfinity(value))
                return "INF";
            if (Single.IsNegativeInfinity(value))
                return "-INF";
            if (Single.IsNaN(value))
                return "NaN";
            StringBuilder sb = new StringBuilder();
            if (value < 0)
            {
                sb.Append('-');
                value = -value;
            }
            int bits = BitConverter.ToInt32(BitConverter.GetBytes(value), 0);
            int fraction = (1 << 23) | (bits & floatFractMask);
            int rawExp = ((bits & floatExpMask) >> floatExpShift);
            int exp = rawExp - floatExpBias;
            if (value < 1E-06F || value >= 1000000F)
                fppfppExponential(sb, exp, fraction, 23);
            else
                fppfpp(sb, exp, fraction, 23);
            return sb.ToString();
        }

        private static void fppfpp(StringBuilder sb, int e, long f, int p)
        {
            long R = f << Math.Max(e - p, 0);
            long S = 1L << Math.Max(0, -(e - p));
            long Mminus = 1L << Math.Max(e - p, 0);
            long Mplus = Mminus;
            bool initial = true;

            // simpleFixup
            if (f == 1L << (p - 1))
            {
                Mplus = Mplus << 1;
                R = R << 1;
                S = S << 1;
            }

            int k = 0;
            while (R < (S + 9) / 10)
            {
                // (S+9)/10 == ceiling(S/10)
                k--;
                R = R * 10;
                Mminus = Mminus * 10;
                Mplus = Mplus * 10;
            }
            while (2 * R + Mplus >= 2 * S)
            {
                S = S * 10;
                k++;
            }

            for (int z = k; z < 0; z++)
            {
                if (initial)
                    sb.Append("0.");
                initial = false;
                sb.Append('0');
            }

            // end simpleFixup

            //int H = k-1;

            bool low;
            bool high;
            int U;
            while (true)
            {
                k--;
                long R10 = R * 10;
                U = (int)(R10 / S);
                R = R10 - (U * S); // = R*10 % S, but faster - saves a division
                Mminus = Mminus * 10;
                Mplus = Mplus * 10;
                low = 2 * R < Mminus;
                high = 2 * R > 2 * S - Mplus;
                if (low || high) break;
                if (k == -1)
                {
                    if (initial)
                        sb.Append('0');
                    sb.Append('.');
                }
                sb.Append(charForDigit[U]);
                initial = false;
            }
            if (high && (!low || 2 * R > S))
            {
                U++;
            }
            if (k == -1)
            {
                if (initial)
                    sb.Append('0');
                sb.Append('.');
            }
            sb.Append(charForDigit[U]);
            for (int z = 0; z < k; z++)
                sb.Append('0');
        }

        private static void fppfppBig(StringBuilder sb, int e, long f, int p)
        {
            //long R = f << Math.max(e-p, 0);
            BigInteger R = new BigInteger(f) << Math.Max(e - p, 0);

            //long S = 1L << Math.max(0, -(e-p));
            BigInteger S = new BigInteger(1) << Math.Max(0, -(e - p));

            //long Mminus = 1 << Math.max(e-p, 0);
            BigInteger Mminus = new BigInteger(1) << Math.Max(e - p, 0);

            //long Mplus = Mminus;
            BigInteger Mplus = Mminus;

            bool initial = true;

            // simpleFixup
            if (f == 1L << (p - 1))
            {
                Mplus = Mplus << 1;
                R = R << 1;
                S = S << 1;
            }
            int k = 0;
            while (R < (S + 9) / 10)
            {
                // (S+9)/10 == ceiling(S/10)
                k--;
                R = R * 10;
                Mminus = Mminus * 10;
                Mplus = Mplus * 10;
            }
            while (2 * R + Mplus >= 2 * S)
            {
                S = S * 10;
                k++;
            }

            for (int z = k; z < 0; z++)
            {
                if (initial)
                    sb.Append("0.");
                initial = false;
                sb.Append('0');
            }

            // end simpleFixup

            //int H = k-1;

            bool low;
            bool high;
            int U;
            while (true)
            {
                k--;
                BigInteger R10 = R * 10;
#if (NET20)
                U = BigInteger.ToInt16(R10 / S);
#else
                U = (int)(R10 / S);
#endif
                R = R10 - (U * S); // = R*10 % S, but faster - saves a division
                Mminus = Mminus * 10;
                Mplus = Mplus * 10;
                low = 2 * R < Mminus;
                high = 2 * R > 2 * S - Mplus;
                if (low || high) break;
                if (k == -1)
                {
                    if (initial)
                        sb.Append('0');
                    sb.Append('.');
                }
                sb.Append(charForDigit[U]);
                initial = false;
            }
            if (high && (!low || 2 * R > S))
            {
                U++;
            }
            if (k == -1)
            {
                if (initial)
                    sb.Append('0');
                sb.Append('.');
            }
            sb.Append(charForDigit[U]);
            for (int z = 0; z < k; z++)
                sb.Append('0');
        }

        private static string fppfppExponential(StringBuilder sb, int e, long f, int p)
        {
            //long R = f << Math.max(e-p, 0);
            //BigInteger R = BigInteger.valueOf(f).shiftLeft(Math.max(e - p, 0));
            BigInteger R = new BigInteger(f) << Math.Max(e - p, 0);

            //long S = 1L << Math.max(0, -(e-p));
            BigInteger S = new BigInteger(1) << Math.Max(0, -(e - p));

            //long Mminus = 1 << Math.max(e-p, 0);
            BigInteger Mminus = new BigInteger(1) << Math.Max(e - p, 0);

            //long Mplus = Mminus;
            BigInteger Mplus = Mminus;

            bool initial = true;
            bool doneDot = false;

            // simpleFixup
            if (f == 1L << (p - 1))
            {
                Mplus = Mplus << 1;
                R = R << 1;
                S = S << 1;
            }
            int k = 0;
            while (R < (S + 9) / 10)
            {
                // (S+9)/10 == ceiling(S/10)
                k--;
                R *= 10;
                Mminus *= 10;
                Mplus *= 10;
            }
            while ((R << 1) + Mplus >= S << 1)
            {
                S *= 10;
                k++;
            }
            // end simpleFixup

            int H = k - 1;

            bool low;
            bool high;

            int U;
            while (true)
            {
                k--;
                BigInteger R10 = R * 10;
#if (NET20)
                U = BigInteger.ToInt16(R10 / S);
#else
                U = (int)(R10 / S);
#endif
                R = R10 % S;
                Mminus *= 10;
                Mplus *= 10;
                BigInteger R2 = R << 1;
                low = R2 < Mminus;
                high = R2 > (S << 1) - Mplus;
                if (low || high)
                    break;
                sb.Append(charForDigit[U]);
                if (initial)
                {
                    sb.Append('.');
                    doneDot = true;
                }
                initial = false;
            }

            if (high && (!low || R << 1 > S))
                U++;
            sb.Append(charForDigit[U]);
            if (!doneDot)
                sb.Append(".0");
            sb.Append('E');
            sb.Append(H.ToString());
            return sb.ToString();
        }
#endif

        public static object ChangeType(XmlSchemaType xmlType, object value, SequenceType type,
            XmlNameTable nameTable, XmlNamespaceManager nsmgr)
        {
            if (type.TypeCode == XmlTypeCode.AnyAtomicType || xmlType.TypeCode == type.TypeCode)
                return value;
            try
            {
                switch (xmlType.TypeCode)
                {
                    case XmlTypeCode.String:
                    case XmlTypeCode.UntypedAtomic:
                        switch (type.TypeCode)
                        {
                            case XmlTypeCode.UntypedAtomic:
                                return new UntypedAtomic(value.ToString());
                            case XmlTypeCode.String:
                                return value.ToString();
                            case XmlTypeCode.DateTime:
                                return DateTimeValue.Parse(value.ToString());
                            case XmlTypeCode.Date:
                                return DateValue.Parse(value.ToString());
                            case XmlTypeCode.Time:
                                return TimeValue.Parse(value.ToString());
                            case XmlTypeCode.GYearMonth:
                                return GYearMonthValue.Parse(value.ToString());
                            case XmlTypeCode.GYear:
                                return GYearValue.Parse(value.ToString());
                            case XmlTypeCode.GMonth:
                                return GMonthValue.Parse(value.ToString());
                            case XmlTypeCode.GMonthDay:
                                return GMonthDayValue.Parse(value.ToString());
                            case XmlTypeCode.GDay:
                                return GDayValue.Parse(value.ToString());
                            case XmlTypeCode.Duration:
                                return DurationValue.Parse(value.ToString());
                            case XmlTypeCode.QName:
                                if (xmlType.TypeCode == XmlTypeCode.UntypedAtomic)
                                    throw new XPath2Exception("XPTY0004", Resources.XPTY0004,
                                        new SequenceType(xmlType, XmlTypeCardinality.One, null), type);
                                return QNameValue.Parse(value.ToString(), nsmgr);
                            case XmlTypeCode.Notation:
                                return NotationValue.Parse(value.ToString(), nsmgr);
                            case XmlTypeCode.AnyUri:
                                return new AnyUriValue(value.ToString());

                            default:
                                {
                                    string text = value.ToString();
                                    object res = type.SchemaType.Datatype.ParseValue(text, nameTable, nsmgr);
                                    switch (type.TypeCode)
                                    {
                                        case XmlTypeCode.Integer:
                                        case XmlTypeCode.PositiveInteger:
                                        case XmlTypeCode.NegativeInteger:
                                        case XmlTypeCode.NonPositiveInteger:
                                        case XmlTypeCode.NonNegativeInteger:
                                            return (Integer)Convert.ToDecimal(res);
                                        case XmlTypeCode.DayTimeDuration:
                                            return new DayTimeDurationValue((TimeSpan)res);
                                        case XmlTypeCode.YearMonthDuration:
                                            return new YearMonthDurationValue((TimeSpan)res);
                                        case XmlTypeCode.HexBinary:
                                            return new HexBinaryValue((byte[])res);
                                        case XmlTypeCode.Base64Binary:
                                            if (text.EndsWith("==") &&
                                                (text.Length < 3 || "AQgw".IndexOf(text[text.Length - 3]) == -1))
                                                throw new XPath2Exception("FORG0001", Resources.FORG0001, value);
                                            return new Base64BinaryValue((byte[])res);
                                        case XmlTypeCode.Idref:
                                            if (type.SchemaType == SequenceType.XmlSchema.IDREFS)
                                                return new IDREFSValue((string[])res);
                                            goto default;
                                        case XmlTypeCode.NmToken:
                                            if (type.SchemaType == SequenceType.XmlSchema.NMTOKENS)
                                                return new NMTOKENSValue((string[])res);
                                            goto default;
                                        case XmlTypeCode.Entity:
                                            if (type.SchemaType == SequenceType.XmlSchema.ENTITIES)
                                                return new ENTITIESValue((string[])res);
                                            goto default;
                                        default:
                                            return res;
                                    }
                                }
                        }

                    case XmlTypeCode.Boolean:
                        switch (type.TypeCode)
                        {
                            case XmlTypeCode.Decimal:
                            case XmlTypeCode.Float:
                            case XmlTypeCode.Double:
                            case XmlTypeCode.Integer:
                            case XmlTypeCode.NonPositiveInteger:
                            case XmlTypeCode.NegativeInteger:
                            case XmlTypeCode.Long:
                            case XmlTypeCode.Int:
                            case XmlTypeCode.Short:
                            case XmlTypeCode.Byte:
                            case XmlTypeCode.NonNegativeInteger:
                            case XmlTypeCode.UnsignedLong:
                            case XmlTypeCode.UnsignedInt:
                            case XmlTypeCode.UnsignedShort:
                            case XmlTypeCode.UnsignedByte:
                            case XmlTypeCode.PositiveInteger:
                                return ChangeType(value, type.ItemType);

                            case XmlTypeCode.String:
                                return ToString((bool)value);
                            case XmlTypeCode.UntypedAtomic:
                                return new UntypedAtomic(ToString((bool)value));
                        }
                        break;

                    case XmlTypeCode.Integer:
                    case XmlTypeCode.NonPositiveInteger:
                    case XmlTypeCode.NegativeInteger:
                    case XmlTypeCode.Long:
                    case XmlTypeCode.Int:
                    case XmlTypeCode.Short:
                    case XmlTypeCode.Byte:
                    case XmlTypeCode.NonNegativeInteger:
                    case XmlTypeCode.UnsignedLong:
                    case XmlTypeCode.UnsignedInt:
                    case XmlTypeCode.UnsignedShort:
                    case XmlTypeCode.UnsignedByte:
                    case XmlTypeCode.PositiveInteger:
                    case XmlTypeCode.Decimal:
                    case XmlTypeCode.Float:
                    case XmlTypeCode.Double:
                        switch (type.TypeCode)
                        {
                            case XmlTypeCode.String:
                                return ToString(value);
                            case XmlTypeCode.UntypedAtomic:
                                return new UntypedAtomic(ToString(value));
                            case XmlTypeCode.Boolean:
                                return CoreFuncs.BooleanValue(value);
                            case XmlTypeCode.AnyUri:
                                throw new XPath2Exception("XPTY0004", Resources.XPTY0004,
                                    new SequenceType(xmlType, XmlTypeCardinality.One, null), type);
                            default:
                                return ChangeType(value, type.ItemType);
                        }

                    default:
                        {
                            IXmlConvertable convert = value as IXmlConvertable;
                            if (convert != null)
                                return convert.ValueAs(type, nsmgr);
                            if (type.TypeCode == XmlTypeCode.String)
                                return ToString(value);
                            if (type.TypeCode == XmlTypeCode.UntypedAtomic)
                                return new UntypedAtomic(ToString(value));
                            return type.SchemaType.Datatype.ChangeType(value, type.ValueType);
                        }
                }
            }
            catch (XmlSchemaException ex)
            {
                throw new XPath2Exception(ex.Message, ex);
            }
            catch (InvalidCastException)
            {
                throw new XPath2Exception("XPTY0004", Resources.XPTY0004,
                    new SequenceType(xmlType, XmlTypeCardinality.One, null), type);
            }
            catch (FormatException)
            {
                throw new XPath2Exception("XPTY0004", Resources.XPTY0004,
                    new SequenceType(xmlType, XmlTypeCardinality.One, null), type);
            }
            throw new XPath2Exception("XPTY0004", Resources.XPTY0004,
                new SequenceType(xmlType, XmlTypeCardinality.One, null), type);
        }

        public static object ChangeType(object value, Type returnType)
        {
            try
            {
                if (returnType == typeof(Object))
                    return value;
                if (returnType == typeof(Integer))
                    return Integer.ToInteger(value);
                return Convert.ChangeType(value, returnType, CultureInfo.InvariantCulture);
            }
            catch (FormatException)
            {
                throw new XPath2Exception("FORG0001", Resources.FORG0001, value,
                    new SequenceType(returnType, XmlTypeCardinality.One));
            }
            catch (OverflowException)
            {
                throw new XPath2Exception("FOAR0002", Resources.FOAR0002, value,
                    new SequenceType(returnType, XmlTypeCardinality.One));
            }
        }

        public static object ValueAs(object value, SequenceType type,
            XmlNameTable nameTable, XmlNamespaceManager nsmgr)
        {
            if (value == Undefined.Value)
                return value;
            if (value == null)
                value = CoreFuncs.False;
            if (type.TypeCode == XmlTypeCode.None)
                throw new XPath2Exception("XPTY0004", Resources.XPTY0004,
                    new SequenceType(value.GetType(), XmlTypeCardinality.One), "empty-sequence()");
            if (value.GetType() != type.ItemType)
            {
                UntypedAtomic untypedAtomic = value as UntypedAtomic;
                if (untypedAtomic != null)
                    return ChangeType(SequenceType.XmlSchema.UntypedAtomic, value, type, nameTable, nsmgr);
                switch (type.TypeCode)
                {
                    case XmlTypeCode.Double:
                        if (value is Single)
                            return Convert.ToDouble((float)value);
                        if (value is Int32)
                            return Convert.ToDouble((int)value);
                        if (value is Int64)
                            return Convert.ToDouble((long)value);
                        if (value is Decimal)
                            return Convert.ToDouble((decimal)value);
                        if (value is Integer)
                            return Convert.ToDouble((decimal)(Integer)value);
                        if (value is Int16)
                            return Convert.ToDouble((short)value);
                        if (value is SByte)
                            return Convert.ToDouble((sbyte)value);
                        break;

                    case XmlTypeCode.Float:
                        if (value is Double)
                            return Convert.ToSingle((double)value);
                        if (value is Int32)
                            return Convert.ToSingle((int)value);
                        if (value is Int64)
                            return Convert.ToSingle((long)value);
                        if (value is Decimal)
                            return Convert.ToSingle((decimal)value);
                        if (value is Integer)
                            return Convert.ToSingle((decimal)(Integer)value);
                        if (value is Int16)
                            return Convert.ToSingle((short)value);
                        if (value is SByte)
                            return Convert.ToSingle((sbyte)value);
                        break;

                    case XmlTypeCode.Integer:
                        if (Integer.IsDerivedSubtype(value))
                            return Integer.ToInteger(value);
                        break;

                    case XmlTypeCode.Decimal:
                        if (value is Integer)
                            return (decimal)(Integer)value;
                        if (Integer.IsDerivedSubtype(value))
                            return (decimal)Integer.ToInteger(value);
                        break;

                    case XmlTypeCode.Int:
                        if (value is Int16)
                            return (int)(short)value;
                        if (value is UInt16)
                            return (int)(ushort)value;
                        if (value is SByte)
                            return (int)(sbyte)value;
                        if (value is Byte)
                            return (int)(byte)value;
                        if (value is Integer)
                            return (int)(Integer)value;
                        break;

                    case XmlTypeCode.Long:
                        if (value is Int32)
                            return (long)(int)value;
                        if (value is Int16)
                            return (long)(short)value;
                        if (value is SByte)
                            return (long)(sbyte)value;
                        if (value is Integer)
                            return (long)(Integer)value;
                        break;
                }
                if (type.TypeCode == XmlTypeCode.AnyUri && value is String)
                    return new AnyUriValue((string)value);
                if (type.TypeCode == XmlTypeCode.String && value is AnyUriValue)
                    return value.ToString();
                if (type.TypeCode == XmlTypeCode.Duration &&
                    (value is YearMonthDurationValue || value is DayTimeDurationValue))
                    return value;
                throw new XPath2Exception("XPTY0004", Resources.XPTY0004,
                    new SequenceType(value.GetType(), XmlTypeCardinality.One), type);
            }
            return value;
        }

        public static object TreatValueAs(object value, SequenceType type)
        {
            if (value == Undefined.Value)
                return value;
            if (value == null)
                value = CoreFuncs.False;
            if (type.TypeCode == XmlTypeCode.None)
                throw new XPath2Exception("XPTY0004", Resources.XPTY0004,
                    new SequenceType(value.GetType(), XmlTypeCardinality.One), "empty-sequence()");
            if (value.GetType() != type.ItemType &&
                type.ItemType != typeof(Object))
            {
                if (type.ItemType == typeof(Integer))
                {
                    if (value is Int32)
                        return (Integer)(int)value;
                    else if (value is UInt32)
                        return (Integer)(uint)value;
                    else if (value is Int64)
                        return (Integer)(long)value;
                    else if (value is UInt64)
                        return (Integer)(ulong)value;
                    else if (value is Int16)
                        return (Integer)(short)value;
                    else if (value is UInt16)
                        return (Integer)(ushort)value;
                    else if (value is SByte)
                        return (Integer)(sbyte)value;
                }
                else if (type.ItemType == typeof(Decimal))
                {
                    if (value is Integer)
                        return (Decimal)(Integer)value;
                    else if (value is Int32)
                        return (Decimal)(int)value;
                    if (value is UInt32)
                        return (Decimal)(uint)value;
                    else if (value is Int64)
                        return (Decimal)(long)value;
                    else if (value is UInt64)
                        return (Decimal)(ulong)value;
                    else if (value is Int16)
                        return (Decimal)(short)value;
                    else if (value is UInt16)
                        return (Decimal)(ushort)value;
                    else if (value is SByte)
                        return (Decimal)(sbyte)value;
                }
                throw new XPath2Exception("XPTY0004", Resources.XPTY0004,
                    new SequenceType(value.GetType(), XmlTypeCardinality.One), type);
            }
            return value;
        }

        public static object GetTypedValue(this XPathItem item)
        {
            XPathNavigator nav = item as XPathNavigator;
            if (nav == null)
                return item.TypedValue;
            IXmlSchemaInfo schemaInfo = nav.SchemaInfo;
            if (schemaInfo == null || schemaInfo.SchemaType == null)
            {
                switch (nav.NodeType)
                {
                    case XPathNodeType.Comment:
                    case XPathNodeType.ProcessingInstruction:
                    case XPathNodeType.Namespace:
                        return nav.Value;
                    default:
                        return new UntypedAtomic(nav.Value);
                }
            }
            XmlTypeCode typeCode = schemaInfo.SchemaType.TypeCode;
            if (typeCode == XmlTypeCode.AnyAtomicType && schemaInfo.MemberType != null)
                typeCode = schemaInfo.MemberType.TypeCode;
            switch (typeCode)
            {
                case XmlTypeCode.UntypedAtomic:
                    return new UntypedAtomic(nav.Value);
                case XmlTypeCode.Integer:
                case XmlTypeCode.PositiveInteger:
                case XmlTypeCode.NegativeInteger:
                case XmlTypeCode.NonPositiveInteger:
                    return (Integer)(decimal)nav.TypedValue;
                case XmlTypeCode.Date:
                    return DateValue.Parse(nav.Value);
                case XmlTypeCode.DateTime:
                    return DateTimeValue.Parse(nav.Value);
                case XmlTypeCode.Time:
                    return TimeValue.Parse(nav.Value);
                case XmlTypeCode.Duration:
                    return DurationValue.Parse(nav.Value);
                case XmlTypeCode.DayTimeDuration:
                    return new DayTimeDurationValue((TimeSpan)nav.TypedValue);
                case XmlTypeCode.YearMonthDuration:
                    return new YearMonthDurationValue((TimeSpan)nav.TypedValue);
                case XmlTypeCode.GDay:
                    return GDayValue.Parse(nav.Value);
                case XmlTypeCode.GMonth:
                    return GMonthValue.Parse(nav.Value);
                case XmlTypeCode.GMonthDay:
                    return GMonthDayValue.Parse(nav.Value);
                case XmlTypeCode.GYear:
                    return GYearValue.Parse(nav.Value);
                case XmlTypeCode.GYearMonth:
                    return GYearMonthValue.Parse(nav.Value);
                case XmlTypeCode.QName:
                case XmlTypeCode.Notation:
                    {
                        XmlNamespaceManager nsmgr = new XmlNamespaceManager(nav.NameTable);
                        ExtFuncs.ScanLocalNamespaces(nsmgr, nav.Clone(), true);
                        if (schemaInfo.SchemaType.TypeCode == XmlTypeCode.Notation)
                            return NotationValue.Parse(nav.Value, nsmgr);
                        else
                            return QNameValue.Parse(nav.Value, nsmgr);
                    }
                case XmlTypeCode.AnyUri:
                    return new AnyUriValue(nav.Value);
                case XmlTypeCode.HexBinary:
                    return new HexBinaryValue((byte[])nav.TypedValue);
                case XmlTypeCode.Base64Binary:
                    return new Base64BinaryValue((byte[])nav.TypedValue);
                case XmlTypeCode.Idref:
                    if (schemaInfo.SchemaType == SequenceType.XmlSchema.IDREFS)
                        return new IDREFSValue((string[])nav.TypedValue);
                    goto default;
                case XmlTypeCode.NmToken:
                    if (schemaInfo.SchemaType == SequenceType.XmlSchema.NMTOKENS)
                        return new NMTOKENSValue((string[])nav.TypedValue);
                    goto default;
                case XmlTypeCode.Entity:
                    if (schemaInfo.SchemaType == SequenceType.XmlSchema.ENTITIES)
                        return new ENTITIESValue((string[])nav.TypedValue);
                    goto default;
                default:
                    return nav.TypedValue;
            }
        }

        public static XmlSchemaType GetSchemaType(this XPathItem item)
        {
            XPathNavigator nav = item as XPathNavigator;
            if (nav != null)
            {
                XmlSchemaType xmlType = nav.XmlType;
                if (xmlType == null)
                    return SequenceType.XmlSchema.UntypedAtomic;
                return xmlType;
            }
            return item.XmlType;
        }
    }
}