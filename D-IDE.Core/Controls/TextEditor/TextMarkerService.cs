﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Rendering;

namespace D_IDE.Core.Controls.Editor
{
	// Copyright (c) AlphaSierraPapa for the SharpDevelop Team
	// This code is distributed under the GNU LGPL

	// Edited by A. Bothe

	/// <summary>
	/// Handles the text markers for a code editor.
	/// </summary>
	public sealed class TextMarkerService : DocumentColorizingTransformer, IBackgroundRenderer
	{
		public readonly TextEditor Editor;
		TextSegmentCollection<TextMarker> markers;
		
		/// <summary>
		/// Initializes the marker service and registers itself with the codeEditor
		/// </summary>
		/// <param name="codeEditor"></param>
		public TextMarkerService(TextEditor codeEditor)
		{
			if (codeEditor == null)
				throw new ArgumentNullException("codeEditor");
			this.Editor = codeEditor;
			markers = new TextSegmentCollection<TextMarker>(Editor.Document);

			var tv = Editor.TextArea.TextView;
			tv.Services.AddService(typeof(TextMarkerService),this);
			tv.LineTransformers.Add(this);
			tv.BackgroundRenderers.Add(this);
		}
				
		#region ITextMarkerService
		public void Add(TextMarker m)
		{
			if (markers.Contains(m))
				return;

			markers.Add(m);
		}

		public TextMarker Create(int startOffset, int length)
		{
			int textLength = Editor.Document.TextLength;
			if (startOffset < 0 || startOffset > textLength)
				throw new ArgumentOutOfRangeException("startOffset", startOffset, "Value must be between 0 and " + textLength);
			if (length < 0 || startOffset + length > textLength)
				throw new ArgumentOutOfRangeException("length", length, "length must not be negative and startOffset+length must not be after the end of the document");
			
			var m = new TextMarker(this, startOffset, length);
			markers.Add(m);
			// no need to mark segment for redraw: the text marker is invisible until a property is set
			return m;
		}
		
		public IEnumerable<TextMarker> GetMarkersAtOffset(int offset)
		{
			return markers.FindSegmentsContaining(offset);
		}
		
		public IEnumerable<TextMarker> TextMarkers {
			get { return markers; }
		}
		
		public void RemoveAll(Predicate<TextMarker> predicate)
		{
			if (predicate == null)
				throw new ArgumentNullException("predicate");
			foreach (TextMarker m in markers.ToArray()) {
				if (predicate(m))
					Remove(m);
			}
		}
		
		public void Remove(TextMarker marker)
		{
			if (marker == null)
				throw new ArgumentNullException("marker");
			var m = marker as TextMarker;
			if (markers.Remove(m)) {
				Redraw(m);
			}
		}
		
		/// <summary>
		/// Redraws the specified text segment.
		/// </summary>
		internal void Redraw(ISegment segment)
		{
			Editor.TextArea.TextView.Redraw(segment, DispatcherPriority.Normal);
		}
		#endregion
		
		#region DocumentColorizingTransformer
		protected override void ColorizeLine(DocumentLine line)
		{
			if (markers == null)
				return;
			int lineStart = line.Offset;
			int lineEnd = lineStart + line.Length;
			foreach (TextMarker marker in markers.FindOverlappingSegments(lineStart, line.Length)) {
				Brush foregroundBrush = null;
				if (marker.ForegroundColor != null) {
					foregroundBrush = new SolidColorBrush(marker.ForegroundColor.Value);
					foregroundBrush.Freeze();
				}
				ChangeLinePart(
					Math.Max(marker.StartOffset, lineStart),
					Math.Min(marker.EndOffset, lineEnd),
					element => {
						if (foregroundBrush != null) {
							element.TextRunProperties.SetForegroundBrush(foregroundBrush);
						}
					}
				);
			}
		}
		#endregion
		
		#region IBackgroundRenderer
		public KnownLayer Layer {
			get {
				// draw behind selection
				return KnownLayer.Selection;
			}
		}
		
		public void Draw(TextView textView, DrawingContext drawingContext)
		{
			if (textView == null)
				throw new ArgumentNullException("textView");
			if (drawingContext == null)
				throw new ArgumentNullException("drawingContext");
			if (markers == null || !textView.VisualLinesValid)
				return;
			var visualLines = textView.VisualLines;
			if (visualLines.Count == 0)
				return;
			int viewStart = visualLines.First().FirstDocumentLine.Offset;
			int viewEnd = visualLines.Last().LastDocumentLine.Offset + visualLines.Last().LastDocumentLine.Length;

			foreach (TextMarker marker in markers.FindOverlappingSegments(viewStart, viewEnd - viewStart)) {
				if (marker.BackgroundColor != null) {
					BackgroundGeometryBuilder geoBuilder = new BackgroundGeometryBuilder();
					geoBuilder.AlignToWholePixels = true;
					geoBuilder.CornerRadius = 3;
					geoBuilder.AddSegment(textView, marker);
					Geometry geometry = geoBuilder.CreateGeometry();
					if (geometry != null) {
						Color color = marker.BackgroundColor.Value;
						SolidColorBrush brush = new SolidColorBrush(color);
						brush.Freeze();
						drawingContext.DrawGeometry(brush, null, geometry);
					}
				}
				if (marker.MarkerType != TextMarkerType.None) {
					foreach (Rect r in BackgroundGeometryBuilder.GetRectsForSegment(textView, marker)) {
						Point startPoint = r.BottomLeft;
						Point endPoint = r.BottomRight;
						
						Pen usedPen = new Pen(new SolidColorBrush(marker.MarkerColor), 1);
						usedPen.Freeze();
						switch (marker.MarkerType) {
							case TextMarkerType.Underlined:
								double offset = 2.5;
								
								int count = Math.Max((int)((endPoint.X - startPoint.X) / offset) + 1, 4);
								
								StreamGeometry geometry = new StreamGeometry();
								
								using (StreamGeometryContext ctx = geometry.Open()) {
									ctx.BeginFigure(startPoint, false, false);
									ctx.PolyLineTo(CreatePoints(startPoint, endPoint, offset, count).ToArray(), true, false);
								}
								
								geometry.Freeze();
								
								drawingContext.DrawGeometry(Brushes.Transparent, usedPen, geometry);
								break;
						}
					}
				}
			}
		}
		
		IEnumerable<Point> CreatePoints(Point start, Point end, double offset, int count)
		{
			for (int i = 0; i < count; i++)
				yield return new Point(start.X + i * offset, start.Y - ((i + 1) % 2 == 0 ? offset : 0));
		}
		#endregion
	}

	public class TextMarker : TextSegment
	{
		public readonly TextMarkerService TextMarkerService;

		/// <summary>
		/// Sets StartOffset and Length to the word's position if WordOffset describes the position of that word.
		/// Alternatively, if WholeLine is set to true, the entire line will be hightlighted
		/// </summary>
		/// <param name="WordOffset"></param>
		/// <param name="WholeLine"></param>
		public void CalculateWordOffset(int WordOffset, bool WholeLine)
		{
			StartOffset = WordOffset;

			var ln = TextMarkerService.Editor.Document.GetLineByOffset(WordOffset);

			if (WholeLine)
			{
				StartOffset = ln.Offset;
				Length = ln.Length;
				return;
			}

			var doc = TextMarkerService.Editor.Document;
			int i = WordOffset;
			bool HadNonWS = false;
			bool IsIdent = false;

			if (i == ln.EndOffset)
			{
				StartOffset = WordOffset;
				Length = 1;
				return;
			}

			while (i < ln.EndOffset)
			{
				var c = doc.GetCharAt(i);
				if (!HadNonWS && !char.IsWhiteSpace(c))
				{
					HadNonWS = true;
					if (char.IsLetterOrDigit(c))
						IsIdent = true;
					StartOffset = i;
				}
				else if (IsIdent)
				{
					if (!char.IsLetterOrDigit(c))
					{
						Length = i - StartOffset;
						break;
					}
				}
				else if (HadNonWS)
					break;
				i++;
			}

			if (!HadNonWS)
			{
				StartOffset = ln.Offset;
				Length = ln.Length;
			}
		}

		public TextMarker(TextMarkerService svc)
		{
			TextMarkerService = svc;
		}

		public TextMarker(TextMarkerService svc, int offset, int length)
		{
			TextMarkerService = svc;
			StartOffset = offset;
			Length = length;
		}

		public TextMarker(TextMarkerService svc, int WordOffset, bool WholeLine)
		{
			TextMarkerService = svc;
			CalculateWordOffset(WordOffset, WholeLine);
		}

		public object Tag { get; set; }

		public Color? BackgroundColor;
		public Color? ForegroundColor;
		public Color MarkerColor=Colors.Red;

		public TextMarkerType MarkerType = TextMarkerType.Underlined;

		public void Delete()
		{
			TextMarkerService.Remove(this);
		}

		public void Redraw()
		{
			TextMarkerService.Redraw(this);
		}

		public string ToolTip { get; set; }
	}

	public enum TextMarkerType
	{
		None,
		Underlined
	}
}
