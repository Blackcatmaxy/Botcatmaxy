# Welcome to BotCatMaxy Contributing guide

We are always happy with people genuinely seeking to contribute in anyway. If you aren't familiar with programming you can still help out with creating well written issues and helping test. There are also guides below on what sections of the code are most newcomer friendly and how to get started. 

Read the [GitHub Doc's Code of Conduct](https://github.com/github/docs/blob/c917cce96bf1e9e58ac86db7f160dcfde03c161a/CODE_OF_CONDUCT.md) for an idea of how we'd like everyone to act here.

## Tools for Success
- When developing (even making small changes) we recommend using [Rider](https://www.jetbrains.com/rider/), [Visual Studio](https://visualstudio.microsoft.com/), or [Visual Studio Code](https://code.visualstudio.com/)
    - These are listed in order of increasing required setup, but also increasing ease of access. No matter what though, setting up even Visual Studio Code will be easier in the long run than trying to use GitHub web editor anything that isn't an IDE that is compatible with C#.
- This project uses MongoDB and if testing you may need to have some tools downloaded.
    - [Community Server](https://www.mongodb.com/try/download/community) may be required for the bot to save data locally.
    - [Compass](https://www.mongodb.com/try/download/compass) is useful for viewing (and editing) the data both locally and if you have access to the server.
    - We are open to migrating from MongoDB, but it is simply not the highest priority and has worked surprisingly well.
    
## Creating a Well Written Issue
- Make sure to search for other issues or PRs (Pull Requests) addressing the topic before continuing.
- Add all appropriate tags such as `bug` or `enhancement` (this is a good time to consider if this will be `difficult` to implement)
    - If it is a `bug` make sure to include information on how to replicate or track down the events in the log in case of an error (context and IDs are useful for both, but timestamps are critical for tracking down logs)
    - For `enhancement` make sure it follows the other established features and philosophy of the bot. We've had many requests that just simply don't fit here like [infraction expiration](https://github.com/Blackcatmaxy/Botcatmaxy/issues/125).
    
## Testing Release Candidate Binaries
If you're interested in helping get a new version out when it's held up by uncertainty of stability:
- Make sure follow the tools guide above.
- Download the ReleaseCandidate/pre-release.
- Head over to the [Discord Developers Page](https://discord.com/developers/applications) and copy a bot token.
    - If you have never done this before you'll need to click `New Application` and insert a name.
    1. Then go to the bot tab and click `Add Bot`.
    2. Then you need to scroll down and under `Privileged Gateway Intents` enable `Server Members Intents`.
    3. You can go back up and copy the token now or do this step after step IV.
        - Go into the downloaded pre-release and open the `BotCatMaxy.ini` file under the `Properties` folder.
        - Paste the token replacing `YOURTOKENHERE`.
    4. Go to the `OAuth2` tab of your Discord Bot page.
        - Specify the `bot` scope.
        - For the permissions you can specify `Admin` or copy the URL and replace `permissions=0&` with `permissions=122813803734&`.
        - then just use the URL to invite the bot to the server you want to test in.

## Areas of Code
- Everything has been oriented around the commands being simple to write, if you have an idea you should be able to look at a similar command, copy it, and add your changes without requiring a full understanding of the system.
    - On a similar note we have gotten testing to a decent place and would benefit from more tests being written for more commands and more cases.
- As of writing this the Temp Act system has been [refactored massively](https://github.com/Blackcatmaxy/Botcatmaxy/pull/141) but in the past it used to be the most troubling piece of code and will likely keep being troubling. Always feel free to suggest better patterns and architecture if you feel like it's pointing to a bad place.
    - On that last note, the [Advanced Permissions System draft](https://github.com/Blackcatmaxy/Botcatmaxy/pull/133) is stalling because of a similar feel. If you think you can improve the design of how the pieces connect, or want to add more parts together, feel free to add another PR as the draft is more to track overall progress.
