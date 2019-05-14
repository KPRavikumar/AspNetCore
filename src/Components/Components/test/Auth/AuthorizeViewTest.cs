// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Security.Claims;
using System.Security.Principal;
using Microsoft.AspNetCore.Components.RenderTree;
using Microsoft.AspNetCore.Components.Test.Helpers;
using Xunit;

namespace Microsoft.AspNetCore.Components
{
    public class AuthorizeViewTest
    {
        [Fact]
        public void RendersNothingIfNotAuthorized()
        {
            // Arrange
            var renderer = new TestRenderer();
            var rootComponent = WrapInAuthorizeView(
                childContent:
                    context => builder => builder.AddContent(0, "This should not be rendered"));

            // Act
            renderer.AssignRootComponentId(rootComponent);
            rootComponent.TriggerRender();

            // Assert
            var diff = renderer.Batches.Single().GetComponentDiffs<AuthorizeView>().Single();
            Assert.Empty(diff.Edits);
        }

        [Fact]
        public void RendersNotAuthorizedContentIfNotAuthorized()
        {
            // Arrange
            var renderer = new TestRenderer();
            var rootComponent = WrapInAuthorizeView(
                childContent:
                    context => builder => builder.AddContent(0, "This should not be rendered"),
                notAuthorizedContent:
                    builder => builder.AddContent(0, "You are not authorized"));

            // Act
            renderer.AssignRootComponentId(rootComponent);
            rootComponent.TriggerRender();

            // Assert
            var diff = renderer.Batches.Single().GetComponentDiffs<AuthorizeView>().Single();
            Assert.Collection(diff.Edits, edit =>
            {
                Assert.Equal(RenderTreeEditType.PrependFrame, edit.Type);
                AssertFrame.Text(
                    renderer.Batches.Single().ReferenceFrames[edit.ReferenceFrameIndex],
                    "You are not authorized");
            });
        }

        [Fact]
        public void RendersNothingIfAuthorizedButNoChildContentOrAuthorizedContentProvided()
        {
            // Arrange
            var renderer = new TestRenderer();
            var rootComponent = WrapInAuthorizeView();
            rootComponent.AuthenticationState = TestAuthState.AuthenticatedAs("Nellie");

            // Act
            renderer.AssignRootComponentId(rootComponent);
            rootComponent.TriggerRender();

            // Assert
            var diff = renderer.Batches.Single().GetComponentDiffs<AuthorizeView>().Single();
            Assert.Empty(diff.Edits);
        }

        [Fact]
        public void RendersChildContentIfAuthorized()
        {
            // Arrange
            var renderer = new TestRenderer();
            var rootComponent = WrapInAuthorizeView(
                childContent: context => builder =>
                    builder.AddContent(0, $"You are authenticated as {context.User.Identity.Name}"));
            rootComponent.AuthenticationState = TestAuthState.AuthenticatedAs("Nellie");

            // Act
            renderer.AssignRootComponentId(rootComponent);
            rootComponent.TriggerRender();

            // Assert
            var diff = renderer.Batches.Single().GetComponentDiffs<AuthorizeView>().Single();
            Assert.Collection(diff.Edits, edit =>
            {
                Assert.Equal(RenderTreeEditType.PrependFrame, edit.Type);
                AssertFrame.Text(
                    renderer.Batches.Single().ReferenceFrames[edit.ReferenceFrameIndex],
                    "You are authenticated as Nellie");
            });
        }

        [Fact]
        public void RendersAuthorizedContentIfAuthorized()
        {
            // Arrange
            var renderer = new TestRenderer();
            var rootComponent = WrapInAuthorizeView(
                authorizedContent: context => builder =>
                    builder.AddContent(0, $"You are authenticated as {context.User.Identity.Name}"));
            rootComponent.AuthenticationState = TestAuthState.AuthenticatedAs("Nellie");

            // Act
            renderer.AssignRootComponentId(rootComponent);
            rootComponent.TriggerRender();

            // Assert
            var diff = renderer.Batches.Single().GetComponentDiffs<AuthorizeView>().Single();
            Assert.Collection(diff.Edits, edit =>
            {
                Assert.Equal(RenderTreeEditType.PrependFrame, edit.Type);
                AssertFrame.Text(
                    renderer.Batches.Single().ReferenceFrames[edit.ReferenceFrameIndex],
                    "You are authenticated as Nellie");
            });
        }

        [Fact]
        public void ThrowsIfBothChildContentAndAuthorizedContentProvided()
        {
            // Arrange
            var renderer = new TestRenderer();
            var rootComponent = WrapInAuthorizeView(
                authorizedContent: context => builder => { },
                childContent: context => builder => { });

            // Act/Assert
            renderer.AssignRootComponentId(rootComponent);
            var ex = Assert.Throws<InvalidOperationException>(() =>
                rootComponent.TriggerRender());
            Assert.Equal("When using AuthorizeView, do not specify both 'Authorized' and 'ChildContent'.", ex.Message);
        }

        [Fact]
        public void RendersNothingUntilAuthorizationCompleted()
        {
            // Arrange
            var renderer = new TestRenderer();
            var rootComponent = WrapInAuthorizeView(
                notAuthorizedContent: builder => builder.AddContent(0, "You are not authorized"));
            rootComponent.AuthenticationState = new PendingAuthenticationState();

            // Act/Assert 1: Auth pending
            renderer.AssignRootComponentId(rootComponent);
            rootComponent.TriggerRender();
            var batch1 = renderer.Batches.Single();
            var authorizeViewComponentId = batch1.GetComponentFrames<AuthorizeView>().Single().ComponentId;
            var diff1 = batch1.DiffsByComponentId[authorizeViewComponentId].Single();
            Assert.Empty(diff1.Edits);

            // Act/Assert 2: Auth process completes asynchronously
            rootComponent.AuthenticationState = new TestAuthState();
            rootComponent.TriggerRender();
            Assert.Equal(2, renderer.Batches.Count);
            var batch2 = renderer.Batches[1];
            var diff2 = batch2.DiffsByComponentId[authorizeViewComponentId].Single();
            Assert.Collection(diff2.Edits, edit =>
            {
                Assert.Equal(RenderTreeEditType.PrependFrame, edit.Type);
                AssertFrame.Text(
                    batch2.ReferenceFrames[edit.ReferenceFrameIndex],
                    "You are not authorized");
            });
        }

        [Fact]
        public void RendersAuthorizingContentUntilAuthorizationCompleted()
        {
            // Arrange
            var renderer = new TestRenderer();
            var rootComponent = WrapInAuthorizeView(
                authorizingContent: builder => builder.AddContent(0, "Auth pending..."),
                authorizedContent: context => builder => builder.AddContent(0, $"Hello, {context.User.Identity.Name}!"));
            rootComponent.AuthenticationState = new PendingAuthenticationState();

            // Act/Assert 1: Auth pending
            renderer.AssignRootComponentId(rootComponent);
            rootComponent.TriggerRender();
            var batch1 = renderer.Batches.Single();
            var authorizeViewComponentId = batch1.GetComponentFrames<AuthorizeView>().Single().ComponentId;
            var diff1 = batch1.DiffsByComponentId[authorizeViewComponentId].Single();
            Assert.Collection(diff1.Edits, edit =>
            {
                Assert.Equal(RenderTreeEditType.PrependFrame, edit.Type);
                AssertFrame.Text(
                    batch1.ReferenceFrames[edit.ReferenceFrameIndex],
                    "Auth pending...");
            });

            // Act/Assert 2: Auth process completes asynchronously
            rootComponent.AuthenticationState = TestAuthState.AuthenticatedAs("Monsieur");
            rootComponent.TriggerRender();
            Assert.Equal(2, renderer.Batches.Count);
            var batch2 = renderer.Batches[1];
            var diff2 = batch2.DiffsByComponentId[authorizeViewComponentId].Single();
            Assert.Collection(diff2.Edits,
            edit =>
            {
                Assert.Equal(RenderTreeEditType.RemoveFrame, edit.Type);
                Assert.Equal(0, edit.SiblingIndex);
            },
            edit =>
            {
                Assert.Equal(RenderTreeEditType.PrependFrame, edit.Type);
                Assert.Equal(0, edit.SiblingIndex);
                AssertFrame.Text(
                    batch2.ReferenceFrames[edit.ReferenceFrameIndex],
                    "Hello, Monsieur!");
            });
        }

        private static TestAuthStateProviderComponent WrapInAuthorizeView(
            RenderFragment<IAuthenticationState> childContent = null,
            RenderFragment<IAuthenticationState> authorizedContent = null,
            RenderFragment notAuthorizedContent = null,
            RenderFragment authorizingContent = null)
        {
            return new TestAuthStateProviderComponent(builder =>
            {
                builder.OpenComponent<AuthorizeView>(0);
                builder.AddAttribute(1, nameof(AuthorizeView.ChildContent), childContent);
                builder.AddAttribute(2, nameof(AuthorizeView.Authorized), authorizedContent);
                builder.AddAttribute(3, nameof(AuthorizeView.NotAuthorized), notAuthorizedContent);
                builder.AddAttribute(4, nameof(AuthorizeView.Authorizing), authorizingContent);
                builder.CloseComponent();
            });
        }

        class TestAuthStateProviderComponent : AutoRenderComponent
        {
            private readonly RenderFragment _childContent;

            public IAuthenticationState AuthenticationState { get; set; } = new TestAuthState();

            public TestAuthStateProviderComponent(RenderFragment childContent)
            {
                _childContent = childContent;
            }

            protected override void BuildRenderTree(RenderTreeBuilder builder)
            {
                builder.OpenComponent<CascadingValue<IAuthenticationState>>(0);
                builder.AddAttribute(1, nameof(CascadingValue<IAuthenticationState>.Value), AuthenticationState);
                builder.AddAttribute(2, RenderTreeBuilder.ChildContent, _childContent);
                builder.CloseComponent();
            }
        }

        class TestAuthState : IAuthenticationState
        {
            public ClaimsPrincipal User { get; set; }

            public bool IsPending => false;

            public static TestAuthState AuthenticatedAs(string username) => new TestAuthState
            {
                User = new ClaimsPrincipal(new TestIdentity { Name = username })
            };

            class TestIdentity : IIdentity
            {
                public string AuthenticationType => "Test";

                public bool IsAuthenticated => true;

                public string Name { get; set; }
            }
        }
    }
}
