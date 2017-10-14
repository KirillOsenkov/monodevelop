//
//  Copyright (c) Microsoft Corporation. All rights reserved.
//  Licensed under the MIT License. See License.txt in the project root for license information.
//
// This file contain implementations details that are subject to change without notice.
// Use at your own risk.
//
namespace Microsoft.VisualStudio.Text.Editor.Implementation
{
	using System;
	using System.Collections.Generic;
	using System.Collections.ObjectModel;

	using Microsoft.VisualStudio.Utilities;
	using Microsoft.VisualStudio.Text.Projection;
	using Microsoft.VisualStudio.Text.Utilities;

	internal class ConnectionManager
	{
		private abstract class BaseListener
		{
			public abstract object ErrorSource { get; }
			public abstract IContentTypeAndTextViewRoleMetadata Metadata { get; }
			public abstract void SubjectBuffersConnected (ITextView textView, ConnectionReason reason, Collection<ITextBuffer> subjectBuffers);
			public abstract void SubjectBuffersDisconnected (ITextView textView, ConnectionReason reason, Collection<ITextBuffer> subjectBuffers);
		}

		private abstract class ListenerCommon<T> : BaseListener
		{
			private readonly Lazy<T, IContentTypeAndTextViewRoleMetadata> importInfo;
			private T listener;
			private readonly GuardedOperations guardedOperations;

			public ListenerCommon (Lazy<T, IContentTypeAndTextViewRoleMetadata> importInfo, GuardedOperations guardedOperations)
			{
				this.importInfo = importInfo;
				this.guardedOperations = guardedOperations;
			}

			public override IContentTypeAndTextViewRoleMetadata Metadata {
				get { return importInfo.Metadata; }
			}

			public T Instance {
				get {
					if (this.listener == null) {
						this.listener = this.guardedOperations.InstantiateExtension (this.importInfo, this.importInfo);
					}
					return this.listener;
				}
			}

			public override object ErrorSource { get { return this.Instance; } }
		}

		private class NonWpfListener : ListenerCommon<ITextViewConnectionListener>
		{
			public NonWpfListener (Lazy<ITextViewConnectionListener, IContentTypeAndTextViewRoleMetadata> importInfo, GuardedOperations guardedOperations)
				: base (importInfo, guardedOperations)
			{
			}

			public override void SubjectBuffersConnected (ITextView textView, ConnectionReason reason, Collection<ITextBuffer> subjectBuffers)
			{
				this.Instance?.SubjectBuffersConnected (textView, reason, subjectBuffers);
			}

			public override void SubjectBuffersDisconnected (ITextView textView, ConnectionReason reason, Collection<ITextBuffer> subjectBuffers)
			{
				this.Instance?.SubjectBuffersDisconnected (textView, reason, subjectBuffers);
			}
		}

		ITextView _textView;
		List<BaseListener> listeners = new List<BaseListener> ();
		GuardedOperations _guardedOperations;

		public ConnectionManager (ITextView textView,
								 ICollection<Lazy<ITextViewConnectionListener, IContentTypeAndTextViewRoleMetadata>> nonWpfTextViewConnectionListeners,
								 GuardedOperations guardedOperations)
		{
			if (textView == null) {
				throw new ArgumentNullException (nameof (textView));
			}
			if (nonWpfTextViewConnectionListeners == null) {
				throw new ArgumentNullException (nameof (nonWpfTextViewConnectionListeners));
			}
			if (guardedOperations == null) {
				throw new ArgumentNullException (nameof (guardedOperations));
			}

			_textView = textView;
			_guardedOperations = guardedOperations;

			List<Lazy<ITextViewConnectionListener, IContentTypeAndTextViewRoleMetadata>> nonWpfFilteredListeners =
								UIExtensionSelector.SelectMatchingExtensions (nonWpfTextViewConnectionListeners, _textView.Roles);
			if (nonWpfFilteredListeners.Count > 0) {
				foreach (var listenerExport in nonWpfFilteredListeners) {
					var listener = new NonWpfListener (listenerExport, guardedOperations);
					this.listeners.Add (listener);

					Collection<ITextBuffer> subjectBuffers =
						textView.BufferGraph.GetTextBuffers (buffer => (Match (listenerExport.Metadata, buffer.ContentType)));

					if (subjectBuffers.Count > 0) {
						var instance = listener.ErrorSource;
						if (instance != null) {
							_guardedOperations.CallExtensionPoint (instance,
																  () => listener.SubjectBuffersConnected (_textView, ConnectionReason.TextViewLifetime, subjectBuffers));
						}
					}
				}
			}

			if (this.listeners.Count > 0) {
				textView.BufferGraph.GraphBuffersChanged += OnGraphBuffersChanged;
				textView.BufferGraph.GraphBufferContentTypeChanged += OnGraphBufferContentTypeChanged;
			}
		}

		public void Close ()
		{
			if (this.listeners.Count > 0) {
				foreach (var listener in this.listeners) {
					Collection<ITextBuffer> subjectBuffers =
						_textView.BufferGraph.GetTextBuffers (buffer => (Match (listener.Metadata, buffer.ContentType)));

					if (subjectBuffers.Count > 0) {
						var instance = listener.ErrorSource;
						if (instance != null) {
							_guardedOperations.CallExtensionPoint (instance,
																  () => listener.SubjectBuffersDisconnected (_textView, ConnectionReason.TextViewLifetime, subjectBuffers));
						}
					}
				}
				_textView.BufferGraph.GraphBuffersChanged -= OnGraphBuffersChanged;
				_textView.BufferGraph.GraphBufferContentTypeChanged -= OnGraphBufferContentTypeChanged;
			}
		}

		private static bool Match (IContentTypeMetadata metadata, IContentType bufferContentType)
		{
			foreach (string listenerContentType in metadata.ContentTypes) {
				if (bufferContentType.IsOfType (listenerContentType)) {
					return true;
				}
			}
			return false;
		}

		private void OnGraphBuffersChanged (object sender, GraphBuffersChangedEventArgs args)
		{
			if (args.AddedBuffers.Count > 0) {
				foreach (var listener in this.listeners) {
					Collection<ITextBuffer> subjectBuffers = new Collection<ITextBuffer> ();
					foreach (ITextBuffer buffer in args.AddedBuffers) {
						if (Match (listener.Metadata, buffer.ContentType)) {
							subjectBuffers.Add (buffer);
						}
					}
					if (subjectBuffers.Count > 0) {
						var instance = listener.ErrorSource;
						if (instance != null) {
							_guardedOperations.CallExtensionPoint (instance,
																  () => listener.SubjectBuffersConnected (_textView, ConnectionReason.BufferGraphChange, subjectBuffers));
						}
					}
				}
			}

			if (args.RemovedBuffers.Count > 0) {
				foreach (BaseListener listener in this.listeners) {
					Collection<ITextBuffer> subjectBuffers = new Collection<ITextBuffer> ();
					foreach (ITextBuffer buffer in args.RemovedBuffers) {
						if (Match (listener.Metadata, buffer.ContentType)) {
							subjectBuffers.Add (buffer);
						}
					}
					if (subjectBuffers.Count > 0) {
						var instance = listener.ErrorSource;
						if (instance != null) {
							_guardedOperations.CallExtensionPoint (instance,
																  () => listener.SubjectBuffersDisconnected (_textView, ConnectionReason.BufferGraphChange, subjectBuffers));
						}
					}
				}
			}
		}

		private void OnGraphBufferContentTypeChanged (object sender, GraphBufferContentTypeChangedEventArgs args)
		{
			var connectedListeners = new List<BaseListener> ();
			var disconnectedListeners = new List<BaseListener> ();

			foreach (BaseListener listener in this.listeners) {
				bool beforeMatch = Match (listener.Metadata, args.BeforeContentType);
				bool afterMatch = Match (listener.Metadata, args.AfterContentType);
				if (beforeMatch != afterMatch) {
					if (listener.ErrorSource != null) {
						if (beforeMatch) {
							disconnectedListeners.Add (listener);
						}
						else {
							connectedListeners.Add (listener);
						}
					}
				}
			}

			Collection<ITextBuffer> subjectBuffers = new Collection<ITextBuffer> (new List<ITextBuffer> (1) { args.TextBuffer });
			foreach (var listener in disconnectedListeners) {
				_guardedOperations.CallExtensionPoint (listener.ErrorSource,
													  () => listener.SubjectBuffersDisconnected (_textView, ConnectionReason.ContentTypeChange, subjectBuffers));
			}

			foreach (var listener in connectedListeners) {
				_guardedOperations.CallExtensionPoint (listener.ErrorSource,
													  () => listener.SubjectBuffersConnected (_textView, ConnectionReason.ContentTypeChange, subjectBuffers));
			}
		}
	}
}
