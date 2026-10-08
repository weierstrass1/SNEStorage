# Resource interactions review — 2026-10-07

## Frontend contracts

- Rating: POST `/api/resource/{resourceId}/Score/Submit/{value}`. The returned ScoreUsers updates the displayed rating. Authors and submitters cannot rate their own resource; private-resource ratings are disabled, matching the service.
- New root comment: POST `/api/resource/{resourceId}/Comment/Submit`, with multipart `commentText`. The confirmed Comment is displayed immediately without refetching the resource. The composer opens with Reply and closes only after confirmed success or Cancel; errors retain the draft.
- Comments: GET `/api/comment/resource/{resourceId}`. Each page requests 20 comments with `page`, `maxAmount` and `sort`; sorting follows CommentSorting. The frontend appends pages as the feed approaches the viewport, retaining existing comments and deduplicating by ID. A Load more button provides keyboard access and a fallback when observation is unavailable. Failed loads preserve the feed and expose an explicit retry.
- Replies: GET `/api/comment/{commentId}/replies`, using the same pagination parameters. Replies load only when expanded. Ancestor checks prevent rendering a cyclic thread without imposing a fixed reply depth.
- Reply: POST `/api/comment/{commentId}/reply`, with multipart `commentText`. The returned CommentComment confirms the operation and supplies the newly posted child, including its user summary. Its editor closes after success, the reply thread expands, and the page scrolls to the confirmed reply. Replies use the same incremental loading as root comments.
- Newly posted comments scroll into view after rendering, receive keyboard focus and a brief highlight. Reduced-motion preferences disable smooth scrolling and the highlight animation. If a new comment falls outside loaded pages under the selected sorting, it remains visible with a Just posted label until a page includes it.
- Refresh and successful edit/delete resynchronize already loaded pages. Changing sorting restarts from the first page. Browser observation is disabled during requests and errors, and is stopped when the component is disposed; stale results cannot update another resource or account.
- Edit: PUT `/api/comment/{commentId}`, with multipart `commentText`.
- Delete: DELETE `/api/comment/{commentId}`. After Success, the comments GET determines whether the row disappeared or remains as a deleted placeholder.
- Vote: POST `/api/comment/{commentId}/like`, with multipart `value` set to Like, Dislike or Remove. Clicking the selected vote sends Remove; clicking the other button changes the vote. Buttons reflect the current user's entry in Score.LikesUsers. They are hidden without a verified session, on deleted comments, and on the current user's own comments, matching CantLikeYourOwnComment; pending requests temporarily disable them. Existing vote counts and approval remain visible regardless of who authored the comment.
- Votes update from the saved LikeUsers response. Counts and approval (`likes / total votes * 100`) are hidden when there are no votes. Failed requests keep the prior displayed vote and ask for a refresh when their result is uncertain.
- GET `/api/comment/{commentId}/userlike` verifies the current user's vote after a lost mutation response. LikeNotFound means no vote; other errors do not clear the displayed vote. The write is never automatically retried.
- The comment card shows publication and last-edit timestamps, avatar, username color and profile link. The replies button appears only when Childrens contains reply relationships; reply text is fetched when expanded. Posting a reply makes it available immediately, and removing the last reply hides the expansion control.
- Edit/delete controls require a verified session and either ownership or the Admin/Moderator claim. The API remains responsible for permission enforcement.
- Deleted comments without children are excluded from the visible feed. Deleted comments with children retain a placeholder and allow existing replies to be expanded, but never show Reply or a reply composer. If the last child is physically removed and the exhausted replies GET is empty, the displayed parent loses its replies indicator and disappears too when it is deleted. Pagination advances using the raw API page size so hidden rows do not prevent loading later comments.
- Failed mutations preserve text. Ambiguous network responses do not cause an automatic retry. Errors are displayed in English without exposing exceptions.
- Razor markup, component state and browser HTTP transport are separate files. Components never query the database or call business services directly.
- Resource categories are displayed from ResourceResourceTypes.ResourceType.Category.

## Backend observations (read only)

### Generic CommentController error response

Corrected by the owner: `GetActionResult<T>` retains its typed return signature, returns `Ok(entity)` for data and sends the extracted enum (`error`) in all other branches. ResourceNotFound and LikeNotFound map to 404; DBError maps to 500.

### Like/dislike mutation

The owner added an authorized action at `/api/comment/{commentId}/like` and the service now adds new LikeUsers to the context and rejects a missing user ID. Both the frontend and the controller now use POST with a FromForm value. GET `/api/comment/{commentId}/userlike` is used for verification after a lost write response; the initial selected vote comes from Score.LikesUsers, avoiding extra reads for every comment. GetUserLike now uses the corrected `if (!res.TryPickT0(...)) return error;` and returns LikeNotFound when no vote exists.

### Resource read size

The detail frontend now obtains comments from their paginated controller action. The ResourceService.Get ResourceComments/User/UserInfo/Avatar Include chain and the corresponding comment FillUserSummaries are no longer needed by this view. Before removing them, check whether other backend callers rely on that graph. They still run until the owner removes them; the frontend alone cannot reduce that original database query.

### Corrected service behavior

- New root comments and replies initialize Score.
- Root posting and reply reading check private-resource permissions.
- Reply creation attaches its own ResourceComments join entity.
- Reply GET filters on Parent.ParentCommentId.
- GET pagination validates enum, amount, page and the long skip calculation.
- The comments GET loads Score.LikesUsers, Childrens and User.UserInfo.Avatar, then fills UserSummary for the returned page.
- DeleteComment loads Childrens, Parent and ResourceComment for the existing ClientCascade mappings. It preserves comments with replies as deleted rows.
- Whitespace-only reply/edit text is rejected.

## Verification limits

`dotnet build SNEStorage/SNEStorage.csproj --no-restore -v quiet` completed with zero errors and five existing warnings in models/migrations. No controller tests, database submissions or browser integration tests were run. Backend files were read, not edited.
