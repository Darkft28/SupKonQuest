const AuthMinPasswordLength = 8;
const AuthUsernameMinLength = 3;
const AuthUsernameMaxLength = 16;

function normalizeUsername(username: string): string {
    if (!username) {
        return '';
    }

    let normalized = username.trim();
    let result = '';
    for (let i = 0; i < normalized.length; i++) {
        const c = normalized.charAt(i);
        if (/[a-zA-Z0-9_-]/.test(c)) {
            result += c;
        }
    }

    if (result.length > AuthUsernameMaxLength) {
        result = result.substring(0, AuthUsernameMaxLength);
    }

    return result;
}

function validateEmailAuth(email: string, password: string, username: string, create: boolean): string | null {
    if (!password || password.length < AuthMinPasswordLength) {
        return 'Password must be at least ' + AuthMinPasswordLength + ' characters.';
    }

    if (!email || email.length < 10 || email.length > 255 || email.indexOf('@') < 0) {
        return 'Invalid email address.';
    }

    if (!create) {
        return null;
    }

    const normalized = normalizeUsername(username);
    if (normalized.length < AuthUsernameMinLength) {
        return 'Username must be ' + AuthUsernameMinLength + '-' + AuthUsernameMaxLength + ' characters (letters, digits, _ or -).';
    }

    return null;
}

function beforeAuthenticateEmail(
    ctx: nkruntime.Context,
    logger: nkruntime.Logger,
    nk: nkruntime.Nakama,
    request: nkruntime.AuthenticateEmailRequest
): nkruntime.AuthenticateEmailRequest | void {
    const email = request.account && request.account.email ? request.account.email : '';
    const password = request.account && request.account.password ? request.account.password : '';
    const username = request.username ? request.username : '';
    const create = request.create === true;

    const error = validateEmailAuth(email, password, username, create);
    if (error) {
        throw error;
    }

    if (create) {
        request.username = normalizeUsername(username);
    }

    return request;
}

function afterAuthenticateEmail(
    ctx: nkruntime.Context,
    logger: nkruntime.Logger,
    nk: nkruntime.Nakama,
    session: nkruntime.Session,
    request: nkruntime.AuthenticateEmailRequest
): void {
    const action = request.create ? 'register' : 'login';
    const userId = ctx.userId ? ctx.userId : '';
    const username = request.username ? request.username : '';
    logger.info('SupKonQuest email auth %s userId=%s username=%s.', action, userId, username);
}
