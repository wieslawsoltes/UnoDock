# Native scroll completion assertion

The first complete visual implementation run, 35788310906, passed all six jobs.
The documentation-only follow-up run, 35789083572, exposed a timing-dependent assertion
in the stationary edge-hover XTEST scenario: the observed final offset moved from
272.2156875 to 275 after its fixed 100 ms delay. The drag timer had already stopped;
a committed reorder can still queue the newly selected tab's visibility adjustment.
No production timer cancellation path was changed to address this test failure.

The test now waits, with a three-second deadline, for actual native release delivery
and the Committed drag state. It independently checks that the drag timer is disabled
and that its last executed scroll-tick timestamp never changes afterward. ViewChanged
intermediate state and horizontal offset updates must then become quiet for 250 ms,
after which the original strict offset-stability assertion is applied over another
100 ms interval. A timer restart or extra scroll tick fails immediately; a view that
never settles also fails. The original stationary-pointer advancement assertion remains.
Event subscriptions are removed in finally, including failure paths. No test is skipped,
retried inside the test, or converted into a warning.

The updated interaction suite passed all 25 cases in the local real Uno/X11 host.
Consume the CI results for the final source revision for full SDK/platform acceptance.
The API inventory, mappings and regression baseline are unchanged by this test-only fix.
