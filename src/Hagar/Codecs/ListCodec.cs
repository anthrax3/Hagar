﻿using System;
using System.Collections.Generic;
using Hagar.Activators;
using Hagar.Buffers;
using Hagar.Serializers;
using Hagar.Session;
using Hagar.WireProtocol;

namespace Hagar.Codecs
{
    /// <summary>
    /// Codec for <see cref="List{T}"/>.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    internal class ListCodec<T> : IFieldCodec<List<T>>
    {
        private readonly IFieldCodec<T> fieldCodec;
        private readonly IFieldCodec<int> intCodec;
        private readonly IUntypedCodecProvider codecProvider;
        private readonly IActivator<int, List<T>> activator;

        public ListCodec(IFieldCodec<T> fieldCodec, IFieldCodec<int> intCodec, IUntypedCodecProvider codecProvider, IActivator<int, List<T>> activator)
        {
            this.fieldCodec = fieldCodec;
            this.intCodec = intCodec;
            this.codecProvider = codecProvider;
            this.activator = activator;
        }

        public void WriteField(Writer writer, SerializerSession session, uint fieldIdDelta, Type expectedType, List<T> value)
        {
            if (ReferenceCodec.TryWriteReferenceField(writer, session, fieldIdDelta, expectedType, value)) return;
            writer.WriteFieldHeader(session, fieldIdDelta, expectedType, value.GetType(), WireType.TagDelimited);

            this.intCodec.WriteField(writer, session, 0, typeof(int), value.Count);
            var first = true;
            foreach (var element in value)
            {
                this.fieldCodec.WriteField(writer, session, first ? 1U : 0, typeof(T), element);
                first = false;
            }

            writer.WriteEndObject();
        }

        public List<T> ReadValue(Reader reader, SerializerSession session, Field field)
        {
            if (field.WireType == WireType.Reference)
                return ReferenceCodec.ReadReference<List<T>>(reader, session, field, this.codecProvider);
            if (field.WireType != WireType.TagDelimited) ThrowUnsupportedWireTypeException(field);

            var placeholderReferenceId = ReferenceCodec.CreateRecordPlaceholder(session);
            List<T> result = null;
            uint fieldId = 0;
            var length = 0;
            var index = 0;
            while (true)
            {
                var header = reader.ReadFieldHeader(session);
                if (header.IsEndBaseOrEndObject) break;
                fieldId += header.FieldIdDelta;
                switch (fieldId)
                {
                    case 0:
                        length = this.intCodec.ReadValue(reader, session, header);
                        result = this.activator.Create(length);
                        result.Capacity = length;
                        ReferenceCodec.RecordObject(session, result, placeholderReferenceId);
                        break;
                    case 1:
                        if (result == null) ThrowLengthFieldMissing();
                        if (index >= length) ThrowIndexOutOfRangeException(length);
                        // ReSharper disable once PossibleNullReferenceException
                        result.Add(this.fieldCodec.ReadValue(reader, session, header));
                        ++index;
                        break;
                    default:
                        reader.ConsumeUnknownField(session, header);
                        break;
                }
            }
            
            return result;
        }

        private static void ThrowUnsupportedWireTypeException(Field field) => throw new UnsupportedWireTypeException(
            $"Only a {nameof(WireType)} value of {WireType.TagDelimited} is supported for string fields. {field}");

        private static void ThrowIndexOutOfRangeException(int length) => throw new IndexOutOfRangeException(
            $"Encountered too many elements in array of type {typeof(List<T>)} with declared length {length}.");

        private static void ThrowLengthFieldMissing() => throw new RequiredFieldMissingException("Serialized array is missing its length field.");
    }
}
