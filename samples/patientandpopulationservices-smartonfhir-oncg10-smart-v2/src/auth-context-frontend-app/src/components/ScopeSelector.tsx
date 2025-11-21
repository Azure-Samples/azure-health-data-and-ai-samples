import React, { useEffect, useState, FC, ReactElement } from 'react';
import { Stack, Text, List, IStackStyles, PrimaryButton, DefaultButton, Checkbox, Spinner, SpinnerSize } from '@fluentui/react';

import { AppConsentInfo, AppConsentScope } from '../AppContext';

const moduleStyle: IStackStyles = {
    root: {
        paddingBottom: 20,
    }
}

interface ScopeSelectorProps {
    consentInfo?: AppConsentInfo
    requestedScopes: string[] | undefined
    updateUserApprovedScopes?: (scopes: AppConsentInfo) => Promise<void>
}

export const ScopeSelector: FC<ScopeSelectorProps> = (props: ScopeSelectorProps): ReactElement => {
    const [consentInfo, setConsentInfo] = useState(props.consentInfo);
    const [requestedScopes, setRequestedScopes] = useState(props.requestedScopes);
    const [mode, setMode] = useState("loading");

    useEffect(() => {
        setConsentInfo(props.consentInfo);

        // #TODO - check other state elements (like update function) before changing state from loading

        // Set the initial state value
        if (props.consentInfo && mode == "loading") {
            if (props.consentInfo.scopes.filter(x => x.consented).length > 0) {
                setMode('existing review');
            }
            else {
                setMode('new edit');
            }
        }
    }, [props]);

    const changeEditMode = () => {
        setMode('existing edit');
    };

    const handleScopeChecked = (scope: AppConsentScope) => {
        return (ev?: React.FormEvent<HTMLElement | HTMLInputElement>, isChecked?: boolean) => {

            if (consentInfo != undefined) {
                // Update the clicked scope, and if we're enabling a parent scope,
                // also uncheck (enabled = false) any granular child scopes whose name starts with the parent's name.
                const updatedScopes = consentInfo.scopes.map(s => {
                    // update the clicked scope itself
                    if (s.name === scope.name && s.resourceId === scope.resourceId) {
                        return { ...s, enabled: !!isChecked };
                    }

                    // If enabling a parent scope, clear any child scopes (UI will also disable them)
                    if (isChecked && s.name.startsWith(scope.name) && s.name !== scope.name) {
                        return { ...s, enabled: false };
                    }

                    // Otherwise keep as-is
                    return s;
                });

                const updateConsentInfo = {
                    ...consentInfo,
                    scopes: updatedScopes,
                };
                setConsentInfo(updateConsentInfo);
            }
        }
    }

    // New helper: compute UI-only disabled state.
    // If any other enabled scope has a name that is a prefix of this scope's name,
    // disable this scope's checkbox in the UI. This does NOT change the underlying scope.enabled values.
    const getIsDisabled = (scope: AppConsentScope): boolean => {
        if (!consentInfo) return false;
        return consentInfo.scopes.some(parent =>
            parent.enabled &&
            parent.name !== scope.name &&
            scope.name.startsWith(parent.name)
        );
    }

    const updateScopes = () => {
        setMode('redirecting');
        props.updateUserApprovedScopes!(consentInfo!);
    };

    return (
        <Stack>
            <Stack.Item align='start'>
                {(mode === 'loading') && <Spinner size={SpinnerSize.large} label="Loading..." ariaLive="assertive" />}
                {(mode === 'redirecting') && <Spinner size={SpinnerSize.large} label="Saving your preferences...this may take a bit..." ariaLive="assertive" />}
            </Stack.Item>

            {(mode.includes('existing') || mode.includes('new')) &&
                <Stack.Item styles={moduleStyle}>
                    <Text block variant="xLarge">Requested Access:</Text>
                    <List items={requestedScopes?.map(x => ({ name: x.replace(/%2f/g, '/') }))} />
                </Stack.Item>
            }

            {mode.includes('existing') &&
                <Stack.Item styles={moduleStyle}>
                    <Text block variant="xLarge">Approved Access:</Text>
                    <List items={consentInfo?.scopes.filter(x => x.consented).filter(x => !x.hidden).map(x => ({ name: x.name.replace(/%2f/g, '/') }))} />
                </Stack.Item>
            }

            {mode.includes('edit') &&
                <Stack.Item styles={moduleStyle}>
                    <Text block variant="xLarge">Select Access:</Text>
                    {consentInfo?.scopes.map((scope) => (
                        scope.hidden ? null : <Checkbox key={scope.id} label={scope.name.replace(/%2f/g, '/')} checked={scope.enabled} onChange={handleScopeChecked(scope)} disabled={getIsDisabled(scope)} />
                    ))}
                </Stack.Item>
            }

            {mode != 'loading' && mode != 'redirecting' &&
                <Stack.Item styles={moduleStyle}>
                    <Stack horizontal>
                        <PrimaryButton text="Continue" onClick={updateScopes} />

                        {mode === 'existing review' && <DefaultButton text="Change Access" onClick={changeEditMode} />}
                    </Stack>
                </Stack.Item>
            }
        </Stack>
    )
}

export default ScopeSelector;
