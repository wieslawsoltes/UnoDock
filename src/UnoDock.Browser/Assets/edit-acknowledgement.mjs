// Acknowledgements carry the committed revision, never a guessed local revision.
// The detached record cannot be used to mutate the authority after the call.
export function acknowledgeEdit(state, owner, request) {
    state.update(owner, request.id, request.lease, request);
    return { ...state.owned(owner, request.id, request.lease) };
}
