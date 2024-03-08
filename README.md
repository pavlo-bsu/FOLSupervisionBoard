# FOLSupervisionBoard
The purpose of the application is to remotely control the fiber-optics module.

## Application features
Broadband analog optical link MOL2000T (Montena &copy;) is chosen as a sample of fibre-optics link.
The application displays optical link level, ID and battery voltage of Receiver and Transmitter modules.
App allows to:
* `Set signal attenuation` in the range 0 dB … 31.75 dB with a step of 0.25dB.
* Set Receiver and Transmitter modules in `low power mode` / normal mode.
* Switch `test signal generator` on / off.
* `Reset` fibre optic link.

As an example, UML sequence diagram presented in the Figure below shows the setting gain (signal attenuation).

![Figure 1]( https://raw.githubusercontent.com/pavlo-bsu/FOLSupervisionBoard/backmatter/img/umlSeqSetGain.png)


## Why some data is updated manually (not automatically by timer, event, etc.)
Update of optical link level, ID and battery voltage of Receiver and Transmitter modules are performed at the user’s command. The same for search of new connected units. These are not done automatically in order to avoid possible loss of the useful physical signal that is currently being transmitted over the fiber: __according to device user manual, disturbance on the analogue link for a few seconds can appear__ while Transmitter and Receiver exchange commands.

When using fibre-optics link as a part of precision measurement complex or critical device, the best strategy seems to be the following sequence of actions:
* Connect all components of fibre-optics link together.
* Check the functionality of the link using “test signal generator” mode.
* Fully setup the link (for example, set a gain).
* Switch Receiver and Transmitter to low power mode while setting up other components of the entire complex that are not related to optical signal transmission.
* During operation of the entire complex, the operator manually forces update of optical link level and charge level of Receiver and transmitter. And do so only at those moments in time when the entire complex does not use the optical subsystem or disturbance on the analogue link for a few seconds is acceptable in operation cycle.

## Other notes
There is no comprehensive data sheet for the fibre-optics link used, so performance is sacrificed for reliability: the Thread.Sleep() method is additionally used when processing device responses.

Extending the functionality of the application is possible by implementing for example the following:
* Increasing of attenuation range up to 62.5 dB. One should implement additional function of the link: power attenuation (31dB).
* Signal preamplification. One should implement additional function of the link: preamplifier (24dB).
