# IpControl

IpControl is a WPF custom control for editing and displaying IPV4 and IPV6 addresses.
Rather than a user control with four text boxes like most implementations, it is a
customization of one textbox to make for easy editing. Although it is designed to be
easy to edit, it nudges the user towards a compliant address. Because it allows a user
to type in an address, during the entry, it does allow an invalid address by way of
being incomplete. However, it does not allow an invalid address to be typed in as far
as too many address components or too high of a value in a component.

If the user deletes characters, it ensures that the current address component (octet
for IPV4, hextet for IPV6) is valid. When the user moves the cursor from one component
to another, either with the keyboard or mouse, the previous component is reformatted.
So if you have an address like "100.100.100.", and your cursor is at the end, but you
move the cursor to the left, the last component will be set to zero, so it will become
"100.100.100.0".

If you delete one of the separators (period on IPV4 and colon on IPV6), then the separator
will be deleted and the component the cursor is on will be reformatted. So if you have
"100.100.100.100" and you delete the last period, it will see that "100100" is not a valid
address component and remove the last three digits.

## Use
Because it inherits from System.Windows.Controls.TextBox, you can use the same XAML
properties you would with a normal text box.

### Relevant Dependency Properties
* Text: This is simply the text that's entered into the text box. If you manually set
this, it is possible to populate the control with an invalid address. I'm considering
hiding the base class version so that I can control what gets put here.
* IPAddress - This is an actual System.Net.IPAddress object. If the address in the text
box is invalid or not complete, this is the closest valid approximation of the address
in the text box.
* IsValidAddress - This is a read-only dependency property that tells whether the text
in the text box is a complete and valid address. Because it's read-only, you can only bind
to it using the mode "OneWay".
